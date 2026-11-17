// Trang thay người: đội trưởng gửi yêu cầu (MAT-05); trọng tài của trận / BTC (fallback) duyệt (MAT-06).
(function () {
    'use strict';

    var MATCH_URL = function (id) { return '/match.html?id=' + id; };
    var POLL_MS = 10000;
    var STATUS = { PENDING: ['Chờ duyệt', 'warn'], APPROVED: ['Đã duyệt', 'ok'], REJECTED: ['Bị từ chối', ''], CANCELLED: ['Đã rút', ''] };

    var esc = Ui.escapeHtml;
    var root = document.getElementById('sbRoot');
    var stateEl = document.getElementById('mdState');
    var matchId = Ui.qsInt('id');
    var b = null, skew = 0, poll = null, tick = null, busy = false, lastSync = 0, flash = null;

    function parseUtc(v) {
        if (!v) return null;
        var d = new Date(/Z|[+-]\d\d:?\d\d$/.test(v) ? v : v + 'Z');
        return isNaN(d) ? null : d.getTime();
    }
    function fmt(ms) { return new Date(ms).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' }); }
    function toast(msg) {
        var t = document.createElement('div'); t.className = 'md-toast'; t.setAttribute('role', 'status'); t.textContent = msg;
        document.body.appendChild(t); setTimeout(function () { t.remove(); }, 3200);
    }
    function now() { return Date.now() + skew; }

    function playerLi(p) { return '<li>' + esc(p.name) + '</li>'; }
    function teamHtml(t, mine) {
        var q = t.used >= t.maxSubstitutions;
        return '<div class="sb-team"><h3><span>' + esc(t.teamName) + (mine ? ' <span class="md-chip ok">Đội của bạn</span>' : '') + '</span>' +
            '<span class="sb-quota' + (q ? ' full' : '') + '">Đã thay ' + t.used + '/' + t.maxSubstitutions + ' lượt</span></h3>' +
            '<div class="sb-group">Tuyển thủ chính</div><ul class="sb-list">' + (t.starters.length ? t.starters.map(playerLi).join('') : '<li class="empty">Chưa có dữ liệu</li>') + '</ul>' +
            '<div class="sb-group">Dự bị</div><ul class="sb-list">' + (t.substitutes.length ? t.substitutes.map(playerLi).join('') : '<li class="empty">Không có dự bị</li>') + '</ul></div>';
    }
    function opts(list) { return list.map(function (p) { return '<option value="' + p.accountId + '">' + esc(p.name) + '</option>'; }).join(''); }

    function itemHtml(it) {
        var st = STATUS[it.status] || [it.status, ''];
        var acts = '';
        if (it.status === 'PENDING') {
            if (b.canDecide) acts += '<input class="note" placeholder="Ghi chú (bắt buộc khi từ chối)" maxlength="300" aria-label="Ghi chú">' +
                '<button type="button" class="md-btn primary" data-act="approve">Duyệt</button><button type="button" class="md-btn" data-act="reject">Từ chối</button>';
            if (b.myTeamId === it.teamId) acts += '<button type="button" class="md-btn" data-act="cancel">Rút yêu cầu</button>';
        }
        return '<div class="sb-item" data-id="' + it.id + '"><div class="row1"><div class="swap">' + esc(it.teamName) + ': ' + esc(it.out.name) + ' → ' + esc(it.in.name) + '</div>' +
            '<span class="md-badge ' + st[1] + '">' + esc(st[0]) + '</span></div>' +
            '<div class="meta">Gửi lúc ' + esc(fmt(parseUtc(it.requestedAt))) + (it.reason ? ' · Lý do: ' + esc(it.reason) : '') +
            (it.decidedByName ? ' · ' + esc(it.decidedByName) + (it.decidedAt ? ' lúc ' + esc(fmt(parseUtc(it.decidedAt))) : '') : '') +
            (it.decisionNote ? ' · Ghi chú: ' + esc(it.decisionNote) : '') + '</div>' + (acts ? '<div class="acts">' + acts + '</div>' : '') + '</div>';
    }

    function render() {
        var mineA = b.myTeamId === b.teamA.teamId, mineB = b.myTeamId === b.teamB.teamId;
        var myTeam = mineA ? b.teamA : mineB ? b.teamB : null;
        var h = '<nav class="md-crumbs"><a href="/">Trang chủ</a><span>›</span><a href="' + esc(MATCH_URL(b.matchId)) + '">Chi tiết trận</a><span>›</span><span>Thay người</span></nav>';
        h += '<section class="md-card"><div class="md-head"><div><div class="md-round">Thay người · ' + esc(b.teamA.teamName) + ' vs ' + esc(b.teamB.teamName) + '</div>' +
            '<div class="md-tour" id="sbWin"></div></div></div>' +
            '<div class="md-notice">Người duyệt: <strong>' + (b.decider === 'REFEREE' ? 'Trọng tài của trận' : 'Ban tổ chức (trận chưa có trọng tài)') + '</strong>. ' +
            'Yêu cầu chỉ gửi được trước giờ thi đấu, trong hạn mức thay của Rule giải.</div>' +
            '<div class="sb-teams">' + teamHtml(b.teamA, mineA) + teamHtml(b.teamB, mineB) + '</div></section>';

        if (b.canRequest && myTeam) {
            var full = myTeam.used >= myTeam.maxSubstitutions;
            var pendingIds = {};
            b.items.forEach(function (i) { if (i.status === 'PENDING' && i.teamId === myTeam.teamId) { pendingIds[i.out.accountId] = 1; pendingIds[i.in.accountId] = 1; } });
            var outs = myTeam.starters.filter(function (p) { return !pendingIds[p.accountId]; });
            var ins = myTeam.substitutes.filter(function (p) { return !pendingIds[p.accountId]; });
            var can = !full && outs.length && ins.length;
            h += '<section class="md-card"><h2>Gửi yêu cầu thay người</h2>' +
                (full ? '<p class="md-muted">Đội đã dùng hết số lần thay tối đa cho trận này.</p>' :
                    !outs.length || !ins.length ? '<p class="md-muted">Không còn cặp tuyển thủ nào để thay (đang chờ duyệt hoặc không có dự bị).</p>' : '') +
                '<div class="sb-form"><div><label class="md-lbl" for="sbOut">Tuyển thủ ra</label><select class="sb-sel" id="sbOut"' + (can ? '' : ' disabled') + '>' + opts(outs) + '</select></div>' +
                '<div><label class="md-lbl" for="sbIn">Dự bị vào</label><select class="sb-sel" id="sbIn"' + (can ? '' : ' disabled') + '>' + opts(ins) + '</select></div>' +
                '<div class="full"><label class="md-lbl" for="sbReason">Lý do (tuỳ chọn) <span class="md-muted" id="sbN">0/300</span></label>' +
                '<textarea id="sbReason" class="md-ta" rows="2" maxlength="300"' + (can ? '' : ' disabled') + '></textarea></div></div>' +
                '<div class="md-notice danger" id="sbErr" role="alert" hidden style="margin-top:10px"></div>' +
                '<div class="md-actions" style="margin-top:12px"><button type="button" class="md-btn primary" id="sbSend"' + (can ? '' : ' disabled') + '>Gửi yêu cầu</button></div></section>';
        }

        h += '<section class="md-card"><h2>Yêu cầu thay người</h2>' + (b.items.length
            ? b.items.slice().reverse().map(itemHtml).join('') : '<p class="md-muted">Chưa có yêu cầu nào.</p>') + '</section>';
        root.innerHTML = h;
        stateEl.hidden = true; root.hidden = false;
        wire(); updateWindow();
    }

    function wire() {
        var send = document.getElementById('sbSend');
        if (send) {
            send.onclick = submitRequest;
            document.getElementById('sbReason').addEventListener('input', function () { document.getElementById('sbN').textContent = this.value.length + '/300'; });
        }
        Array.prototype.forEach.call(root.querySelectorAll('[data-act]'), function (btn) { btn.onclick = onItemAction; });
    }

    function updateWindow() {
        var el = document.getElementById('sbWin'); if (!el || !b) return;
        var sched = parseUtc(b.scheduledAt);
        el.textContent = sched == null ? 'Chưa có giờ thi đấu'
            : b.matchStatus !== 'READY' ? 'Trận không còn ở trạng thái sẵn sàng'
                : now() < sched ? 'Giờ đấu ' + fmt(sched) + ' · còn ' + Math.max(1, Math.ceil((sched - now()) / 60000)) + ' phút để gửi yêu cầu'
                    : 'Đã quá giờ thi đấu — không nhận yêu cầu mới';
        if (sched != null && b.requestWindowOpen && now() >= sched && Date.now() - lastSync > 3000) load(true);   // qua giờ đấu → đồng bộ lại
    }

    async function submitRequest() {
        if (busy) return;
        var err = document.getElementById('sbErr'), btn = document.getElementById('sbSend'); err.hidden = true;
        busy = true; btn.disabled = true; btn.textContent = 'Đang gửi…';
        try {
            await Api.post('/api/matches/' + matchId + '/substitutions', {
                outAccountId: parseInt(document.getElementById('sbOut').value, 10),
                inAccountId: parseInt(document.getElementById('sbIn').value, 10),
                reason: document.getElementById('sbReason').value.trim() || null
            });
            toast('Đã gửi yêu cầu, chờ duyệt.');
        } catch (e) {
            busy = false; err.textContent = (e && e.message) || 'Không gửi được yêu cầu.'; err.hidden = false;
            await load(true); var er = document.getElementById('sbErr'); if (er) { er.textContent = (e && e.message) || ''; er.hidden = false; }
            return;
        }
        busy = false; await load(true);
    }

    async function onItemAction() {
        if (busy) return;
        var act = this.getAttribute('data-act'), item = this.closest('.sb-item'), id = item.getAttribute('data-id');
        var note = item.querySelector('input.note');
        var noteVal = note ? note.value.trim() : '';
        if (act === 'reject' && noteVal.length < 5) { toast('Từ chối phải kèm ghi chú (tối thiểu 5 ký tự).'); if (note) note.focus(); return; }
        busy = true; Array.prototype.forEach.call(item.querySelectorAll('button'), function (x) { x.disabled = true; });
        try {
            if (act === 'cancel') await Api.post('/api/matches/' + matchId + '/substitutions/' + id + '/cancel');
            else await Api.post('/api/matches/' + matchId + '/substitutions/' + id + '/decision', { approve: act === 'approve', note: noteVal || null });
            toast(act === 'approve' ? 'Đã duyệt yêu cầu.' : act === 'reject' ? 'Đã từ chối yêu cầu.' : 'Đã rút yêu cầu.');
        } catch (e) { toast((e && e.message) || 'Không thực hiện được.'); }
        busy = false; await load(true);
    }

    function fail(msg, retry) {
        root.hidden = true; stateEl.hidden = false;
        stateEl.innerHTML = '<div class="md-error"><h1>' + esc(msg) + '</h1>' + (retry ? '<button type="button" class="md-btn primary" id="mdRetry">Thử lại</button>'
            : '<a class="md-btn" href="' + esc(MATCH_URL(matchId || 0)) + '">Về trang trận đấu</a>') + '</div>';
        var r = document.getElementById('mdRetry'); if (r) r.onclick = function () { stateEl.innerHTML = '<div class="md-skel md-skel-head"></div><div class="md-skel md-skel-body"></div>'; load(false); };
    }

    async function load(silent) {
        try {
            b = await Api.get('/api/matches/' + matchId + '/substitutions');
            lastSync = Date.now();
            var srv = parseUtc(b.serverNow); if (srv != null) skew = srv - Date.now();
            // Không dựng lại khi người dùng đang gõ trong biểu mẫu / ô ghi chú (làm mới nền)
            var ae = document.activeElement;
            var typing = silent && ae && root.contains(ae) && /^(TEXTAREA|INPUT|SELECT)$/.test(ae.tagName) && ae.value !== '';
            if (!typing) render();
            clearTimeout(poll); poll = setTimeout(function () { load(true); }, POLL_MS);
        } catch (e) {
            if (silent && b) { clearTimeout(poll); poll = setTimeout(function () { load(true); }, POLL_MS); return; }
            if (e && e.status === 403) fail('Bạn không có quyền xem thay người của trận này.', false);
            else if (e && e.status === 404) fail('Không tìm thấy trận đấu.', false);
            else if (e && e.status === 409) fail('Trận chưa xác định đủ hai đội.', false);
            else fail((e && e.message) || 'Không tải được dữ liệu.', true);
        }
    }

    (async function init() {
        if (!matchId) { fail('Đường dẫn không hợp lệ.', false); return; }
        var me = await Ui.requireAuth(); if (!me) return;
        await load(false);
        tick = setInterval(updateWindow, 15000);
    })();
})();