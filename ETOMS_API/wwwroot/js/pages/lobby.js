// Phòng chờ trận (MAT-07): chat trực tiếp qua SignalR; toàn bộ log được lưu phía máy chủ làm bằng chứng.
(function () {
    'use strict';

    var MATCH_URL = function (id) { return '/match.html?id=' + id; };
    var HUB_URL = '/hubs/match-chat';
    var MAX = 1000;
    var POLL_FALLBACK_MS = 5000;
    var ROLE = { CAPTAIN: 'Đội trưởng', ORGANIZER: 'BTC', REFEREE: 'Trọng tài', ADMIN: 'Admin' };
    var ERR = {
        FORBIDDEN: 'Bạn không có quyền trong phòng chờ này.', LOBBY_CLOSED: 'Phòng chờ đã đóng, không gửi thêm được.',
        EMPTY_MESSAGE: 'Tin nhắn đang trống.', TOO_LONG: 'Tin nhắn tối đa ' + MAX + ' ký tự.',
        RATE_LIMITED: 'Bạn gửi quá nhanh, vui lòng chậm lại một chút.', NOT_FOUND: 'Không tìm thấy trận đấu.', UNAUTHORIZED: 'Phiên đăng nhập đã hết hạn.'
    };

    var esc = Ui.escapeHtml;
    var root = document.getElementById('lbRoot');
    var stateEl = document.getElementById('mdState');
    var matchId = Ui.qsInt('id');
    var h = null;                  // ChatHistory
    var msgs = [];                 // theo Id tăng dần
    var ids = {};                  // dedupe
    var conn = null, connState = 'connecting', poll = null, sending = false, myId = null;

    function toast(msg) {
        var t = document.createElement('div'); t.className = 'md-toast'; t.setAttribute('role', 'status'); t.textContent = msg;
        document.body.appendChild(t); setTimeout(function () { t.remove(); }, 3200);
    }
    function parseUtc(v) {
        if (!v) return null;
        var d = new Date(/Z|[+-]\d\d:?\d\d$/.test(v) ? v : v + 'Z');
        return isNaN(d) ? null : d;
    }
    function hm(d) { return d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }); }
    function dayKey(d) { return d.getFullYear() + '-' + d.getMonth() + '-' + d.getDate(); }
    function dayLabel(d) { return d.toLocaleDateString('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit' }); }
    function errText(e) {
        var m = e && e.message || '';
        var c = /(NOT_FOUND|FORBIDDEN|LOBBY_CLOSED|EMPTY_MESSAGE|TOO_LONG|RATE_LIMITED|UNAUTHORIZED)/.exec(m);
        if (c) return ERR[c[1]];
        if (e && e.status === 429) return ERR.RATE_LIMITED;
        if (e && e.status === 409) return ERR.LOBBY_CLOSED;
        return m && !/HubException|Error:/.test(m) ? m : 'Không gửi được tin nhắn.';
    }

    // ---------- dữ liệu ----------
    function merge(list) {
        var added = false;
        list.forEach(function (m) { if (!ids[m.id]) { ids[m.id] = 1; msgs.push(m); added = true; } });
        if (added) msgs.sort(function (a, b) { return a.id - b.id; });
        return added;
    }

    // ---------- giao diện ----------
    function msgHtml(m, prev) {
        var d = parseUtc(m.sentAt), mine = m.senderId === myId;
        var sep = '';
        var pd = prev ? parseUtc(prev.sentAt) : null;
        if (d && (!pd || dayKey(d) !== dayKey(pd))) sep = '<div class="lb-day">' + esc(dayLabel(d)) + '</div>';
        var who = (ROLE[m.senderRole] || m.senderRole) + (m.teamName ? ' ' + m.teamName : '');
        return sep + '<div class="lb-msg ' + (mine ? 'mine ' : '') + m.senderRole.toLowerCase() + '" data-id="' + m.id + '">' +
            '<div class="lb-meta"><strong>' + esc(m.senderName) + '</strong><span class="lb-role">' + esc(who) + '</span><time>' + (d ? esc(hm(d)) : '') + '</time></div>' +
            '<div class="lb-bubble">' + esc(m.text).replace(/\n/g, '<br>') + '</div></div>';
    }
    function listHtml() {
        if (!msgs.length) return '<div class="lb-empty">Chưa có tin nhắn. Hãy trao đổi Room ID, mật khẩu phòng hoặc báo trễ giờ tại đây.</div>';
        return msgs.map(function (m, i) { return msgHtml(m, msgs[i - 1]); }).join('');
    }
    function connHtml() {
        var map = { connecting: ['Đang kết nối…', 'warn'], live: ['Trực tiếp', 'ok'], reconnecting: ['Mất kết nối, đang thử lại…', 'warn'], fallback: ['Chế độ dự phòng (làm mới 5 giây)', 'warn'], closed: ['Đã ngắt kết nối', ''] };
        var c = map[connState] || map.closed;
        return '<span class="md-badge ' + c[1] + '" id="lbConn">' + esc(c[0]) + '</span>';
    }

    function render() {
        var parts = h.participants.map(function (p) {
            return '<span class="md-chip' + (p.accountId === myId ? ' ok' : '') + '">' + esc(p.name) + ' · ' + esc(ROLE[p.role] || p.role) + (p.teamName ? ' ' + esc(p.teamName) : '') + '</span>';
        }).join(' ');
        var canExport = h.myRole !== 'CAPTAIN';
        var out = '<nav class="md-crumbs"><a href="/">Trang chủ</a><span>›</span><a href="' + esc(MATCH_URL(h.matchId)) + '">Chi tiết trận</a><span>›</span><span>Phòng chờ</span></nav>';
        out += '<section class="md-card lb-card"><div class="md-head"><div><div class="md-round">Phòng chờ · ' + esc(h.teamAName) + ' vs ' + esc(h.teamBName) + '</div>' +
            '<div class="md-tour">Trao đổi Room ID, mật khẩu phòng, báo trễ giờ</div></div><div class="lb-head-r">' + connHtml() +
            (canExport ? '<button type="button" class="md-btn" id="lbExport">Tải log</button>' : '') + '</div></div>' +
            '<div class="lb-parts">' + parts + '</div>' +
            '<div class="md-notice">Toàn bộ nội dung chat được lưu lại và dùng làm bằng chứng khi xử lý tranh chấp. Tin nhắn không thể sửa hoặc xoá.</div>' +
            (h.closedReason === 'READ_ONLY' ? '<div class="md-notice">Bạn đang xem ở chế độ chỉ đọc.</div>' :
                h.closedReason === 'LOBBY_CLOSED' ? '<div class="md-notice danger" id="lbClosed">Phòng chờ đã đóng (trận đã kết thúc hoặc chưa mở). Bạn vẫn xem được lịch sử.</div>' : '') +
            '<div class="lb-log" id="lbLog" role="log" aria-live="polite" tabindex="0">' +
            (h.hasMore ? '<button type="button" class="md-btn lb-more" id="lbMore">Tải tin cũ hơn</button>' : '') + listHtml() + '</div>' +
            '<button type="button" class="md-btn primary lb-new" id="lbNew" hidden>Có tin nhắn mới ↓</button>' +
            (h.canSend ? '<div class="lb-compose"><div class="lb-quick"><button type="button" class="md-btn" data-tpl="Đội mình xin báo trễ khoảng … phút, mong BTC thông cảm.">Báo trễ giờ</button></div>' +
                '<div class="lb-row"><textarea id="lbText" class="md-ta" rows="2" maxlength="' + MAX + '" placeholder="Nhập tin nhắn… (Enter để gửi, Shift+Enter xuống dòng)" aria-label="Tin nhắn"></textarea>' +
                '<button type="button" class="md-btn primary" id="lbSend">Gửi</button></div>' +
                '<div class="lb-count md-muted"><span id="lbN">0</span>/' + MAX + '</div></div>' : '') + '</section>';
        root.innerHTML = out;
        stateEl.hidden = true; root.hidden = false;
        wire(); scrollEnd(true);
    }

    function log() { return document.getElementById('lbLog'); }
    function nearEnd() { var l = log(); return !l || l.scrollHeight - l.scrollTop - l.clientHeight < 80; }
    function scrollEnd(force) { var l = log(); if (l && (force || nearEnd())) { l.scrollTop = l.scrollHeight; var n = document.getElementById('lbNew'); if (n) n.hidden = true; } }

    function refreshList(stick) {
        var l = log(); if (!l) return;
        var more = document.getElementById('lbMore');
        var keep = more ? more.outerHTML : '';
        var wasNear = nearEnd();
        l.innerHTML = keep + listHtml();
        wireMore();
        if (stick || wasNear) scrollEnd(true); else { var n = document.getElementById('lbNew'); if (n) n.hidden = false; }
    }
    function setConn(s) {
        connState = s;
        var el = document.getElementById('lbConn'); if (!el) return;
        var tmp = document.createElement('div'); tmp.innerHTML = connHtml(); el.replaceWith(tmp.firstChild);
    }

    function wireMore() {
        var m = document.getElementById('lbMore'); if (m) m.onclick = loadOlder;
    }
    function wire() {
        wireMore();
        var t = document.getElementById('lbText'), s = document.getElementById('lbSend');
        if (t) {
            t.addEventListener('input', function () { document.getElementById('lbN').textContent = t.value.length; });
            t.addEventListener('keydown', function (e) { if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); send(); } });
        }
        if (s) s.onclick = send;
        Array.prototype.forEach.call(root.querySelectorAll('[data-tpl]'), function (b) {
            b.onclick = function () { if (t) { t.value = b.getAttribute('data-tpl'); t.dispatchEvent(new Event('input')); t.focus(); t.setSelectionRange(t.value.indexOf('…'), t.value.indexOf('…') + 1); } };
        });
        var n = document.getElementById('lbNew'); if (n) n.onclick = function () { scrollEnd(true); };
        var l = log(); if (l) l.addEventListener('scroll', function () { if (nearEnd()) { var nb = document.getElementById('lbNew'); if (nb) nb.hidden = true; } });
        var ex = document.getElementById('lbExport'); if (ex) ex.onclick = exportLog;
    }

    // ---------- thao tác ----------
    async function loadOlder() {
        var b = document.getElementById('lbMore'); if (!b || !msgs.length) return;
        b.disabled = true; b.textContent = 'Đang tải…';
        try {
            var r = await Api.get('/api/matches/' + matchId + '/chat?before=' + msgs[0].id + '&limit=100');
            var l = log(), prevH = l.scrollHeight;
            merge(r.messages); h.hasMore = r.hasMore;
            if (!r.hasMore) b.remove();
            l.innerHTML = (r.hasMore ? '<button type="button" class="md-btn lb-more" id="lbMore">Tải tin cũ hơn</button>' : '') + listHtml();
            wireMore(); l.scrollTop = l.scrollHeight - prevH;
        } catch (e) { b.disabled = false; b.textContent = 'Tải tin cũ hơn'; toast((e && e.message) || 'Không tải được tin cũ.'); }
    }

    async function send() {
        if (sending) return;
        var t = document.getElementById('lbText'), btn = document.getElementById('lbSend');
        var text = t.value.trim();
        if (!text) { t.focus(); return; }
        if (text.length > MAX) { toast(ERR.TOO_LONG); return; }
        sending = true; btn.disabled = true;
        try {
            var dto;
            if (conn && connState === 'live') dto = await conn.invoke('Send', matchId, text);
            else dto = await Api.post('/api/matches/' + matchId + '/chat', { text: text });
            if (merge([dto && (dto.data || dto)].filter(Boolean))) refreshList(true);
            t.value = ''; document.getElementById('lbN').textContent = '0';
        } catch (e) {
            toast(errText(e));
            if (/LOBBY_CLOSED/.test((e && e.message) || '') || (e && e.status === 409)) load(true);
        }
        sending = false; btn.disabled = false; t.focus();
    }

    async function exportLog() {
        var b = document.getElementById('lbExport'); b.disabled = true;
        try {
            var res = await Auth.apiFetch('/api/matches/' + matchId + '/chat/export', { credentials: 'same-origin' });
            if (!res.ok) throw new Error('Không tải được log.');
            var blob = await res.blob(), a = document.createElement('a');
            a.href = URL.createObjectURL(blob); a.download = 'match-' + matchId + '-chat.txt'; document.body.appendChild(a); a.click();
            setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 500);
        } catch (e) { toast((e && e.message) || 'Không tải được log.'); }
        b.disabled = false;
    }

    // ---------- kết nối ----------
    function onMessage(m) { if (m && m.matchId === matchId && merge([m])) refreshList(false); }

    function startFallback() {
        setConn('fallback'); clearInterval(poll);
        poll = setInterval(function () { load(true); }, POLL_FALLBACK_MS);
    }

    async function connect() {
        if (!h.canSend && h.closedReason !== null && h.closedReason !== undefined && h.closedReason !== 'READ_ONLY') { setConn('closed'); return; }
        if (typeof window.signalR === 'undefined') { startFallback(); return; }
        try {
            conn = new signalR.HubConnectionBuilder()
                .withUrl(HUB_URL, { accessTokenFactory: function () { return (window.Auth && Auth.getAccessToken && Auth.getAccessToken()) || ''; } })
                .withAutomaticReconnect([0, 1000, 3000, 8000, 15000]).build();
            conn.on('message', onMessage);
            conn.onreconnecting(function () { setConn('reconnecting'); });
            conn.onreconnected(async function () {
                try { await conn.invoke('JoinMatch', matchId); await load(true); setConn('live'); } catch (e) { startFallback(); }
            });
            conn.onclose(function () { if (connState !== 'closed') startFallback(); });
            await conn.start();
            await conn.invoke('JoinMatch', matchId);
            await load(true);                  // bù tin nhắn trong khoảng giữa lúc tải trang và lúc vào phòng
            setConn('live');
        } catch (e) { try { conn && conn.stop(); } catch (x) { } conn = null; startFallback(); }
    }

    // ---------- khởi tạo ----------
    function fail(msg, retry) {
        root.hidden = true; stateEl.hidden = false;
        stateEl.innerHTML = '<div class="md-error"><h1>' + esc(msg) + '</h1>' + (retry ? '<button type="button" class="md-btn primary" id="mdRetry">Thử lại</button>'
            : '<a class="md-btn" href="' + esc(MATCH_URL(matchId || 0)) + '">Về trang trận đấu</a>') + '</div>';
        var r = document.getElementById('mdRetry'); if (r) r.onclick = function () { stateEl.innerHTML = '<div class="md-skel md-skel-head"></div><div class="md-skel md-skel-body"></div>'; init(); };
    }

    async function load(silent) {
        try {
            var r = await Api.get('/api/matches/' + matchId + '/chat?limit=100');
            var first = !h;
            var prevHasMore = h ? h.hasMore : r.hasMore;
            h = r;
            if (!first) h.hasMore = prevHasMore;      // tải lại 100 tin mới nhất không cho biết còn tin cũ hơn phần đã nạp
            var added = merge(r.messages);
            if (first) render();
            else {
                var canNow = h.canSend, hadCompose = !!document.getElementById('lbText');
                if (hadCompose !== canNow) { var keepText = hadCompose ? document.getElementById('lbText').value : ''; render(); var t = document.getElementById('lbText'); if (t && keepText) t.value = keepText; }
                else if (added) refreshList(false);
            }
        } catch (e) {
            if (silent && h) return;
            if (e && e.status === 403) fail('Chỉ đội trưởng hai đội, BTC và trọng tài của trận mới vào được phòng chờ.', false);
            else if (e && e.status === 404) fail('Không tìm thấy trận đấu.', false);
            else fail((e && e.message) || 'Không tải được phòng chờ.', true);
            throw e;
        }
    }

    async function init() {
        if (!matchId) { fail('Đường dẫn không hợp lệ.', false); return; }
        var me = await Ui.requireAuth(); if (!me) return;
        myId = me.accountId || me.id || null;
        try { await load(false); } catch (e) { return; }
        if (myId == null) myId = (h.participants.filter(function (p) { return p.role === h.myRole && (h.myTeamId == null || p.teamId === h.myTeamId); })[0] || {}).accountId || null;
        await connect();
    }
    window.addEventListener('beforeunload', function () { connState = 'closed'; try { conn && conn.stop(); } catch (e) { } });
    init();
})();