// Trang check-in cho đội trưởng: nút tự mở trước giờ đấu 30 phút, đóng đúng giờ đấu; đếm ngược theo đồng hồ máy chủ.
(function () {
    'use strict';

    var MATCH_URL = function (id) { return '/match.html?id=' + id; };
    var POLL_MS = 8000;
    var LEAD_MS = 30 * 60 * 1000;

    var esc = Ui.escapeHtml;
    var root = document.getElementById('ciRoot');
    var stateEl = document.getElementById('mdState');
    var matchId = Ui.qsInt('id');

    var st = null;          // trạng thái mới nhất từ máy chủ
    var skew = 0;           // serverNow - clientNow (ms)
    var tick = null, poll = null, busy = false, flash = null, lastSync = 0;

    function parseUtc(v) {
        if (!v) return null;
        var d = new Date(/Z|[+-]\d\d:?\d\d$/.test(v) ? v : v + 'Z');
        return isNaN(d) ? null : d.getTime();
    }
    function fmtTime(ms) {
        return new Date(ms).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }
    function fmtCountdown(ms) {
        if (ms < 0) ms = 0;
        var s = Math.floor(ms / 1000), d = Math.floor(s / 86400); s -= d * 86400;
        var h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), sec = s % 60;
        var p = function (n) { return (n < 10 ? '0' : '') + n; };
        return (d ? d + ' ngày ' : '') + p(h) + ':' + p(m) + ':' + p(sec);
    }
    function initials(n) { return (n || '?').trim().split(/\s+/).slice(0, 2).map(function (x) { return x.charAt(0).toUpperCase(); }).join(''); }
    function now() { return Date.now() + skew; }

    // Pha tính ở trình duyệt (theo đồng hồ máy chủ) để nút mở/đóng đúng giây mà không cần chờ lần tải kế tiếp.
    function livePhase() {
        var sched = parseUtc(st.scheduledAt);
        if (sched == null) return 'NOT_SCHEDULED';
        var n = now();
        if (n < sched - LEAD_MS) return 'NOT_YET';
        return n < sched ? 'OPEN' : 'CLOSED';
    }

    function teamBox(t, label, me) {
        if (!t) return '<div class="ci-team"><span class="who">' + label + '</span><div class="md-logo" aria-hidden="true">?</div><div class="md-tname">Chưa xác định</div></div>';
        var logo = Ui.safeUrl(t.logoUrl) ? '<img class="md-logo" src="' + esc(Ui.safeUrl(t.logoUrl)) + '" alt="" loading="lazy">'
            : '<div class="md-logo" aria-hidden="true">' + esc(initials(t.name)) + '</div>';
        return '<div class="ci-team' + (me ? ' me' : '') + '"><span class="who">' + label + '</span>' + logo +
            '<div class="md-tname">' + esc(t.name) + '</div>' +
            '<span class="ci-status' + (t.checkedIn ? ' ok' : '') + '" data-team="' + (me ? 'me' : 'opp') + '">' +
            (t.checkedIn ? '✓ Đã check-in' : 'Chưa check-in') + '</span></div>';
    }

    function render() {
        var d = st;
        document.title = 'Check-in · ' + d.myTeam.name + ' vs ' + (d.opponent ? d.opponent.name : 'TBD') + ' · ETOMS';
        root.innerHTML =
            '<nav class="md-crumbs"><a href="/">Trang chủ</a><span>›</span><a href="' + esc(MATCH_URL(d.matchId)) + '">Chi tiết trận</a><span>›</span><span>Check-in</span></nav>' +
            '<section class="md-card"><div class="md-head"><div><div class="md-round">Check-in điểm danh</div><div class="md-tour">' + esc(d.tournamentName) +
            (d.scheduledAt ? ' · Giờ đấu ' + esc(fmtTime(parseUtc(d.scheduledAt))) : '') + '</div></div></div>' +
            '<div class="ci-clock" id="ciClock" aria-live="off"></div><div class="ci-bar" id="ciBarWrap" hidden><i id="ciBar"></i></div>' +
            '<div class="ci-teams">' + teamBox(d.myTeam, 'Đội của bạn', true) + teamBox(d.opponent, 'Đối thủ', false) + '</div>' +
            '<div class="ci-cta"><button type="button" class="md-btn primary" id="ciBtn"></button><div class="ci-msg" id="ciMsg" role="status" aria-live="polite"></div></div></section>' +
            '<p class="md-muted" style="text-align:center">Check-in mở trước giờ thi đấu 30 phút và đóng đúng giờ thi đấu.</p>';
        document.getElementById('ciBtn').onclick = doCheckIn;
        stateEl.hidden = true; root.hidden = false;
        update();
    }

    // Cập nhật phần động (đồng hồ, nút, trạng thái đối thủ) mỗi giây — không dựng lại cả trang.
    function update() {
        if (!st) return;
        var phase = livePhase(), sched = parseUtc(st.scheduledAt), n = now();
        var clock = document.getElementById('ciClock'), btn = document.getElementById('ciBtn'), msg = document.getElementById('ciMsg');
        var barWrap = document.getElementById('ciBarWrap'), bar = document.getElementById('ciBar');
        if (!clock) return;
        var mine = st.myTeam.checkedIn;
        var lbl, time = '', sub = '', urgent = false;
        barWrap.hidden = true;

        if (st.reason === 'NOT_READY') { lbl = 'Trận chưa sẵn sàng'; time = '—'; sub = st.matchStatus === 'READY' ? 'Chưa xác định đối thủ.' : 'Trận không còn ở trạng thái chờ thi đấu.'; }
        else if (phase === 'NOT_SCHEDULED') { lbl = 'Chưa có giờ thi đấu'; time = '—'; sub = 'Check-in sẽ mở khi ban tổ chức xếp lịch.'; }
        else if (phase === 'NOT_YET') { lbl = 'Check-in mở sau'; time = fmtCountdown(sched - LEAD_MS - n); sub = 'Mở lúc ' + fmtTime(sched - LEAD_MS); }
        else if (phase === 'OPEN') {
            lbl = mine ? 'Trận bắt đầu sau' : 'Check-in đóng sau'; time = fmtCountdown(sched - n);
            sub = 'Đóng lúc ' + fmtTime(sched); urgent = sched - n < 5 * 60 * 1000;
            barWrap.hidden = false; bar.style.width = Math.max(0, Math.min(100, ((sched - n) / LEAD_MS) * 100)) + '%';
        } else { lbl = 'Check-in đã đóng'; time = '00:00:00'; sub = mine ? 'Đội của bạn đã check-in.' : 'Đội của bạn đã không check-in kịp.'; urgent = !mine; }

        clock.className = 'ci-clock' + (urgent ? ' urgent' : '');
        clock.innerHTML = '<div class="lbl">' + esc(lbl) + '</div><div class="t">' + esc(time) + '</div><div class="sub">' + esc(sub) + '</div>';

        var canNow = !mine && phase === 'OPEN' && st.reason !== 'NOT_READY';
        btn.disabled = busy || !canNow;
        btn.textContent = busy ? 'Đang check-in…' : mine ? '✓ Đã check-in' : phase === 'OPEN' ? 'Check-in ngay' : phase === 'CLOSED' ? 'Đã hết hạn check-in' : 'Chưa đến giờ check-in';
        btn.classList.toggle('primary', canNow || busy);
        if (flash) { msg.className = 'ci-msg ' + flash.cls; msg.textContent = flash.text; }
        else if (mine && st.opponent) { msg.className = 'ci-msg ok'; msg.textContent = st.opponent.checkedIn ? 'Cả hai đội đã check-in. Chờ trọng tài bắt đầu trận.' : 'Đang chờ đối thủ check-in…'; }
        else { msg.className = 'ci-msg'; msg.textContent = ''; }

        // Qua mốc mở/đóng → tải lại để đồng bộ với máy chủ.
        if (phase !== st.phase && !busy && Date.now() - lastSync > 3000) load(true);
    }

    function apply(data) {
        st = data; lastSync = Date.now();
        var srv = parseUtc(data.serverNow);
        if (srv != null) skew = srv - Date.now();
    }

    async function load(silent) {
        try {
            apply(await Api.get('/api/matches/' + matchId + '/check-in'));
            if (document.getElementById('ciClock')) {
                // Chỉ đổi phần cần thiết khi tải nền để không nháy trang
                var needFull = !document.getElementById('ciBtn') || silent === false;
                if (needFull) render(); else { refreshTeams(); update(); }
            } else render();
            schedulePoll();
        } catch (e) {
            if (silent && st) { schedulePoll(); return; }
            if (e && e.status === 403) fail('Chỉ đội trưởng của một trong hai đội mới được check-in trận này.', false);
            else if (e && e.status === 404) fail('Không tìm thấy trận đấu.', false);
            else fail((e && e.message) || 'Không tải được dữ liệu check-in.', true);
        }
    }

    function refreshTeams() {
        var me = document.querySelector('[data-team=me]'), opp = document.querySelector('[data-team=opp]');
        function set(el, t) { if (!el || !t) return; el.className = 'ci-status' + (t.checkedIn ? ' ok' : ''); el.textContent = t.checkedIn ? '✓ Đã check-in' : 'Chưa check-in'; }
        set(me, st.myTeam); set(opp, st.opponent);
    }

    function schedulePoll() {
        clearTimeout(poll);
        if (!st || st.phase === 'CLOSED' || st.reason === 'NOT_READY') return;
        poll = setTimeout(function () { if (!document.hidden) load(true); else schedulePoll(); }, POLL_MS);
    }

    async function doCheckIn() {
        if (busy || !st) return;
        busy = true; flash = null; update();
        try {
            apply(await Api.post('/api/matches/' + matchId + '/check-in'));
            flash = { cls: 'ok', text: 'Check-in thành công!' };
            refreshTeams();
        } catch (e) {
            flash = { cls: 'err', text: (e && e.message) || 'Check-in thất bại.' };
            busy = false; await load(true);   // 409: đồng bộ lại trạng thái (đã check-in / hết giờ…)
        }
        busy = false; update(); schedulePoll();
        setTimeout(function () { flash = null; update(); }, 5000);
    }

    function fail(msg, retry) {
        root.hidden = true; stateEl.hidden = false;
        stateEl.innerHTML = '<div class="md-error"><h1>' + esc(msg) + '</h1>' +
            (retry ? '<button type="button" class="md-btn primary" id="mdRetry">Thử lại</button>'
                : '<a class="md-btn" href="' + esc(MATCH_URL(matchId || 0)) + '">Về trang trận đấu</a>') + '</div>';
        var r = document.getElementById('mdRetry');
        if (r) r.onclick = function () { stateEl.innerHTML = '<div class="md-skel md-skel-head"></div><div class="md-skel md-skel-body"></div>'; load(false); };
    }

    (async function init() {
        if (!matchId) { fail('Đường dẫn không hợp lệ.', false); return; }
        var me = await Ui.requireAuth();          // chưa đăng nhập → chuyển tới trang đăng nhập
        if (!me) return;
        await load(false);
        tick = setInterval(update, 1000);
    })();
})();