// Trang chi tiết trận đấu (khách xem được; đăng nhập thì có thêm nút thao tác theo role).
(function () {
    'use strict';

    // ---- Cấu hình điểm tích hợp với các module khác -------------------------------------------------
    // Mỗi thao tác trỏ tới trang/chức năng thuộc module riêng. Chưa có trang thì để null → hiện thông báo.
    var TOURNAMENT_URL = function (id) { return '/tournament.html?id=' + id; };
    var LOGIN_URL = 'auth/login.html';
    var ACTION_ROUTES = {
        CHECK_IN: function (d) { return '/checkin.html?id=' + d.matchId; },
        SUBMIT_REPORT: null,
        OPEN_DISPUTE: null,
        REFEREE_START: null,
        REFEREE_SCORE: null,
        MANAGE: null,
        MANUAL_CHECKIN: null,  // xử lý ngay trên trang bằng hộp thoại (openManualCheckIn)
        SUBSTITUTION: function (d) { return '/substitution.html?id=' + d.matchId; },
        REVIEW_SUBSTITUTION: function (d) { return '/substitution.html?id=' + d.matchId; },
        LOBBY: function (d) { return '/lobby.html?id=' + d.matchId; }
    };
    var ACTION_LABELS = {
        CHECK_IN: ['Check-in', true],
        SUBMIT_REPORT: ['Báo cáo kết quả', true],
        OPEN_DISPUTE: ['Khiếu nại / báo lỗi', false],
        REFEREE_START: ['Bắt đầu trận', true],
        REFEREE_SCORE: ['Nhập tỉ số', true],
        MANAGE: ['Quản lý trận', false],
        MANUAL_CHECKIN: ['Check-in thủ công', true],
        SUBSTITUTION: ['Thay người', false],
        REVIEW_SUBSTITUTION: ['Duyệt thay người', true],
        LOBBY: ['Phòng chờ chat', false]
    };
    var STATUS = {
        PENDING: ['Chờ xác định đội', ''], READY: ['Sẵn sàng', 'info'], LIVE: ['Đang diễn ra', 'live'],
        WAITING_REPORT: ['Chờ báo cáo kết quả', 'warn'], DISPUTED: ['Đang tranh chấp', 'warn'],
        VERIFIED: ['Đã xác nhận', 'ok'], WALKOVER: ['Thắng do đối thủ bỏ cuộc', 'ok'], CANCELLED: ['Đã hủy', '']
    };
    var SOURCE = { AUTO_MATCHED: 'Hai đội báo cáo trùng khớp', VAR_RULING: 'Phán quyết của trọng tài / BTC', WALKOVER: 'Xử thắng (bỏ cuộc)' };
    var DISPUTE = { OPEN: 'Đang mở', UNDER_REVIEW: 'Đang xem xét', RESOLVED: 'Đã giải quyết', CLOSED: 'Đã đóng' };
    var POLL_MS = 20000;

    var esc = Ui.escapeHtml;
    var root = document.getElementById('mdRoot');
    var stateEl = document.getElementById('mdState');
    var matchId = Ui.qsInt('id');
    var timer = null;
    var lastData = null;

    function fmtDate(v) {
        if (!v) return null;
        var d = new Date(/Z|[+-]\d\d:?\d\d$/.test(v) ? v : v + 'Z'); // API trả UTC
        return isNaN(d) ? null : d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }
    function initials(name) {
        return (name || '?').trim().split(/\s+/).slice(0, 2).map(function (p) { return p.charAt(0).toUpperCase(); }).join('');
    }
    function fmtSize(n) {
        if (!n) return '';
        return n >= 1048576 ? (n / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.round(n / 1024)) + ' KB';
    }
    function toast(msg) {
        var t = document.createElement('div');
        t.className = 'md-toast'; t.setAttribute('role', 'status'); t.textContent = msg;
        document.body.appendChild(t);
        setTimeout(function () { t.remove(); }, 3200);
    }

    function teamHtml(t, showScore) {
        var tbd = !t.teamId;
        var cls = 'md-team' + (tbd ? ' md-tbd' : '') + (t.isWinner ? ' win' : '');
        var logo = !tbd && Ui.safeUrl(t.logoUrl)
            ? '<img class="md-logo" src="' + esc(Ui.safeUrl(t.logoUrl)) + '" alt="" loading="lazy">'
            : '<div class="md-logo" aria-hidden="true">' + esc(tbd ? '?' : initials(t.name)) + '</div>';
        var name = tbd ? 'Chưa xác định' : esc(t.name);
        return '<div class="' + cls + '">' + logo +
            '<div class="md-tname">' + name + '</div>' +
            (t.tag ? '<div class="md-ttag">[' + esc(t.tag) + ']</div>' : '') +
            (t.isWinner ? '<div class="md-crown">★ Thắng</div>' : '') +
            (!tbd && showScore ? '<span class="md-chip' + (t.checkedIn ? ' ok' : '') + '">' + (t.checkedIn ? 'Đã check-in' : 'Chưa check-in') + '</span>' : '') +
            '</div>';
    }

    function render(d) {
        lastData = d;
        document.title = (d.teamA.name || 'TBD') + ' vs ' + (d.teamB.name || 'TBD') + ' · ETOMS';
        var st = STATUS[d.status] || [d.status, ''];
        var hasScore = d.scoreA != null && d.scoreB != null;
        var showCheck = d.status === 'READY';
        var h = [];

        h.push('<nav class="md-crumbs" aria-label="Điều hướng"><a href="/">Trang chủ</a><span>›</span>' +
            '<a href="' + esc(TOURNAMENT_URL(d.tournament.tournamentId)) + '">' + esc(d.tournament.name) + '</a><span>›</span><span>' + esc(d.round.label) + '</span></nav>');

        if (d.dispute) {
            var open = d.dispute.status === 'OPEN' || d.dispute.status === 'UNDER_REVIEW';
            h.push('<div class="md-notice danger" role="status"><strong>Trận có tranh chấp</strong> — ' + esc(DISPUTE[d.dispute.status] || d.dispute.status) +
                (open ? '. Kết quả có thể thay đổi sau phán quyết.' : '.') + '</div>');
        }

        if (d.noShow) {
            var names = d.noShow.teamIds.map(function (id) { return id === d.teamA.teamId ? d.teamA.name : id === d.teamB.teamId ? d.teamB.name : '#' + id; });
            h.push('<div class="md-notice danger" role="status"><strong>Cờ No-show</strong> — ' + esc(names.join(', ')) +
                (d.noShow.kind === 'DOUBLE' ? ' đều vắng mặt, trận đang chờ BTC quyết định.' : ' vắng mặt, trận đã xử thắng trắng cho đối thủ.') +
                ' Nếu lý do đi trễ được chấp thuận, dùng “Check-in thủ công” để gỡ cờ và cho thi đấu tiếp.</div>');
        }

        h.push('<section class="md-card" aria-label="Thông tin trận"><div class="md-head"><div>' +
            '<div class="md-round">' + esc(d.round.label) + '</div><div class="md-tour">' + esc(d.tournament.name) + '</div></div>' +
            '<span class="md-badge ' + st[1] + '">' + esc(st[0]) + '</span></div>' +
            '<div class="md-vs">' + teamHtml(d.teamA, showCheck) +
            '<div class="md-mid">' +
            (hasScore ? '<div class="md-score" aria-label="Tỉ số ' + d.scoreA + ' ' + d.scoreB + '">' + d.scoreA + '<small>–</small>' + d.scoreB + '</div>'
                : '<div class="md-vs-label">VS</div>') +
            '<div class="md-bo">BO' + d.bestOf + '</div></div>' +
            teamHtml(d.teamB, showCheck) + '</div></section>');

        // Thông tin chung
        var info = [];
        info.push(['Giờ thi đấu', esc(fmtDate(d.scheduledAt) || 'Chưa xếp lịch')]);
        info.push(['Thể thức', 'Best of ' + d.bestOf]);
        info.push(['Vòng đấu', esc(d.round.label)]);
        var ref = d.referee
            ? '<span class="md-ref">' + (Ui.safeUrl(d.referee.avatarUrl) ? '<img src="' + esc(Ui.safeUrl(d.referee.avatarUrl)) + '" alt="">' : '<span class="md-av">' + esc(initials(d.referee.fullName)) + '</span>') + esc(d.referee.fullName) + '</span>'
            : '<span class="md-muted">Chưa phân công</span>';
        info.push(['Trọng tài phụ trách', ref]);
        if (fmtDate(d.checkInOpenAt) && (d.status === 'PENDING' || d.status === 'READY')) info.push(['Mở check-in', esc(fmtDate(d.checkInOpenAt))]);
        if (fmtDate(d.reportDeadlineAt) && (d.status === 'LIVE' || d.status === 'WAITING_REPORT')) info.push(['Hạn báo cáo kết quả', esc(fmtDate(d.reportDeadlineAt))]);
        if (d.resultSource) info.push(['Nguồn kết quả', esc(SOURCE[d.resultSource] || d.resultSource)]);
        if (fmtDate(d.verifiedAt)) info.push(['Xác nhận lúc', esc(fmtDate(d.verifiedAt))]);
        h.push('<section class="md-card"><h2>Thông tin</h2><dl class="md-info">' + info.map(function (r) {
            return '<div><dt>' + esc(r[0]) + '</dt><dd>' + r[1] + '</dd></div>';
        }).join('') + '</dl>' +
            (Ui.safeUrl(d.tournament.streamUrl) && d.status === 'LIVE' ? '<p style="margin:14px 0 0"><a class="md-btn primary" target="_blank" rel="noopener noreferrer" href="' + esc(Ui.safeUrl(d.tournament.streamUrl)) + '">Xem trực tiếp</a></p>' : '') +
            '</section>');

        if (d.room) {
            h.push('<section class="md-card"><h2>Phòng đấu</h2><div class="md-room">' +
                (d.room.code ? '<div>Mã phòng: <code>' + esc(d.room.code) + '</code></div>' : '') +
                (d.room.password ? '<div>Mật khẩu: <code>' + esc(d.room.password) + '</code></div>' : '') + '</div></section>');
        }

        if (d.games && d.games.length) {
            h.push('<section class="md-card"><h2>Kết quả từng ván</h2><table class="md-table"><thead><tr><th>Ván</th><th>' + esc(d.teamA.name || 'Đội A') +
                '</th><th>' + esc(d.teamB.name || 'Đội B') + '</th></tr></thead><tbody>' + d.games.map(function (g) {
                    var a = g.winnerTeamId && g.winnerTeamId === d.teamA.teamId, b = g.winnerTeamId && g.winnerTeamId === d.teamB.teamId;
                    return '<tr><td>' + g.gameNo + '</td><td class="' + (a ? 'w' : '') + '">' + (g.scoreA == null ? '–' : g.scoreA) +
                        '</td><td class="' + (b ? 'w' : '') + '">' + (g.scoreB == null ? '–' : g.scoreB) + '</td></tr>';
                }).join('') + '</tbody></table></section>');
        }

        // Minh chứng
        var evi;
        if (!d.evidenceVisible) evi = '<p class="md-muted">Minh chứng sẽ được công khai sau khi trận được xác nhận kết quả.</p>';
        else if (!d.evidence.length) evi = '<p class="md-muted">Chưa có minh chứng nào.</p>';
        else evi = '<div class="md-evi">' + d.evidence.map(function (e) {
            var url = Ui.safeUrl(e.fileUrl); if (!url) return '';
            var thumb = e.fileType === 'IMAGE'
                ? '<div class="thumb" style="background-image:url(\'' + esc(url).replace(/'/g, '%27') + '\')" role="img" aria-label="Ảnh minh chứng"></div>'
                : '<div class="thumb">' + esc(e.fileType) + '</div>';
            var team = e.teamId === d.teamA.teamId ? d.teamA.name : e.teamId === d.teamB.teamId ? d.teamB.name : (e.source === 'DISPUTE' ? 'Khiếu nại' : '');
            return '<a href="' + esc(url) + '" target="_blank" rel="noopener noreferrer">' + thumb +
                '<span class="cap">' + esc(team || '') + (e.sizeBytes ? ' · ' + fmtSize(e.sizeBytes) : '') + '</span></a>';
        }).join('') + '</div>';
        h.push('<section class="md-card"><h2>Minh chứng</h2>' + evi + '</section>');

        // Nút thao tác
        var acts = d.actions || [];
        var guest = d.viewer.role === 'GUEST';
        if (acts.length || (guest && d.status !== 'VERIFIED' && d.status !== 'WALKOVER' && d.status !== 'CANCELLED')) {
            h.push('<section class="md-card"><h2>Thao tác</h2><div class="md-actions">' + (guest
                ? '<a class="md-btn primary" href="' + esc(LOGIN_URL + '?returnUrl=' + encodeURIComponent(location.pathname + location.search)) + '">Đăng nhập để thao tác</a>'
                : acts.map(function (a) {
                    var l = ACTION_LABELS[a] || [a, false];
                    return '<button type="button" class="md-btn' + (l[1] ? ' primary' : '') + '" data-action="' + esc(a) + '">' + esc(l[0]) + '</button>';
                }).join('')) + '</div></section>');
        }

        root.innerHTML = h.join('');
        stateEl.hidden = true; root.hidden = false;
        schedulePoll(d.status);
    }

    root.addEventListener('click', function (e) {
        var b = e.target.closest('[data-action]');
        if (!b || !lastData) return;
        var act = b.getAttribute('data-action');
        if (act === 'MANUAL_CHECKIN') { openManualCheckIn(lastData); return; }
        var route = ACTION_ROUTES[act];
        if (typeof route === 'function') { location.href = route(lastData); return; }
        toast('Chức năng này chưa được tích hợp vào trang.');
    });

    // ---- Check-in thủ công (MAT-04): chỉ BTC / admin thấy nút; máy chủ kiểm tra lại quyền ----
    function openManualCheckIn(d) {
        var cand = (d.noShow ? d.noShow.teamIds : [d.teamA, d.teamB].filter(function (t) { return t.teamId && !t.checkedIn; }).map(function (t) { return t.teamId; }));
        var nameOf = function (id) { return id === d.teamA.teamId ? d.teamA.name : id === d.teamB.teamId ? d.teamB.name : '#' + id; };
        var dlg = document.createElement('dialog');
        dlg.className = 'md-dialog';
        dlg.innerHTML = '<form method="dialog" id="mciForm" novalidate><h2>Check-in thủ công</h2>' +
            '<p class="md-muted">' + (d.noShow ? 'Gỡ cờ No-show và cho đội tiếp tục thi đấu.' : 'Đánh dấu đội đã có mặt.') + ' Lý do sẽ được ghi vào Audit Log.</p>' +
            '<fieldset class="md-fs"><legend>Đội được check-in</legend>' + cand.map(function (id) {
                return '<label class="md-check"><input type="checkbox" name="team" value="' + id + '" checked> ' + esc(nameOf(id)) + '</label>';
            }).join('') + '</fieldset>' +
            '<label class="md-lbl" for="mciReason">Lý do được chấp thuận <span class="md-muted" id="mciN">0/500</span></label>' +
            '<textarea id="mciReason" maxlength="500" rows="3" class="md-ta" placeholder="Vd: Đội báo trễ 10 phút do sự cố mạng, đã xác nhận với trọng tài."></textarea>' +
            '<div class="md-notice danger" id="mciErr" role="alert" hidden></div>' +
            '<div class="md-actions" style="justify-content:flex-end;margin-top:12px"><button type="button" class="md-btn" id="mciCancel">Hủy</button>' +
            '<button type="button" class="md-btn primary" id="mciOk">Xác nhận</button></div></form>';
        document.body.appendChild(dlg);
        var $ = function (id) { return dlg.querySelector(id); };
        var close = function () { dlg.close(); dlg.remove(); };
        $('#mciCancel').onclick = close;
        dlg.addEventListener('cancel', function () { dlg.remove(); });
        $('#mciReason').addEventListener('input', function () { $('#mciN').textContent = this.value.length + '/500'; });
        $('#mciOk').onclick = async function () {
            var err = $('#mciErr'), btn = this, reason = $('#mciReason').value.trim();
            var teams = Array.prototype.map.call(dlg.querySelectorAll('input[name=team]:checked'), function (x) { return parseInt(x.value, 10); });
            err.hidden = true;
            if (!teams.length) { err.textContent = 'Chọn ít nhất một đội.'; err.hidden = false; return; }
            if (reason.length < 10) { err.textContent = 'Lý do phải từ 10 ký tự.'; err.hidden = false; return; }
            btn.disabled = true; btn.textContent = 'Đang xử lý…';
            try {
                var r = await Api.post('/api/organizer/matches/' + d.matchId + '/manual-check-in', { teamIds: teams, reason: reason });
                close();
                toast(r && r.noShowFlagRemoved ? 'Đã gỡ cờ No-show, trận được tiếp tục.' : 'Đã check-in thủ công.');
                load(true);
            } catch (e) {
                err.textContent = (e && e.message) || 'Không thực hiện được.'; err.hidden = false;
                btn.disabled = false; btn.textContent = 'Xác nhận';
            }
        };
        dlg.showModal();
        $('#mciReason').focus();
    }

    function showError(msg, retry) {
        root.hidden = true; stateEl.hidden = false;
        stateEl.innerHTML = '<div class="md-error"><h1>' + esc(msg) + '</h1>' +
            (retry ? '<button type="button" class="md-btn primary" id="mdRetry">Thử lại</button>' : '<a class="md-btn" href="/">Về trang chủ</a>') + '</div>';
        var r = document.getElementById('mdRetry'); if (r) r.onclick = function () { stateEl.innerHTML = '<div class="md-skel md-skel-head"></div><div class="md-skel md-skel-body"></div>'; load(); };
    }

    function schedulePoll(status) {
        clearTimeout(timer);
        if (status === 'LIVE' || status === 'READY' || status === 'WAITING_REPORT')
            timer = setTimeout(function () { if (!document.hidden) load(true); else schedulePoll(status); }, POLL_MS);
    }

    async function load(silent) {
        try {
            var d = await Api.get('/api/matches/' + matchId);
            render(d);
        } catch (e) {
            if (silent && lastData) { schedulePoll(lastData.status); return; }   // lỗi thoáng qua khi tự làm mới: giữ nguyên trang
            if (e && e.status === 404) showError('Không tìm thấy trận đấu', false);
            else showError((e && e.message) || 'Không tải được dữ liệu trận đấu', true);
        }
    }

    if (!matchId) showError('Đường dẫn trận đấu không hợp lệ', false);
    else load();
})();