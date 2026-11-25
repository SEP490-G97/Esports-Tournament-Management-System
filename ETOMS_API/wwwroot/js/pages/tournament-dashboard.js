(function () {
    var tournamentId = null;

    function showEmpty(title, message) {
        document.getElementById('dashboard-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    // Server trả giờ UTC không kèm múi giờ.
    function formatTime(value) {
        if (!value) return '';
        var date = new Date(/Z$/.test(value) ? value : value + 'Z');
        return Ui.fmtDateTime(date);
    }

    function formatMoney(value) {
        return Number(value).toLocaleString('vi-VN') + ' đ';
    }

    function statCard(label, value, sub, style) {
        return '<div class="col"><div class="stat-card h-100"><div class="label">' + Ui.escapeHtml(label) + '</div>' +
            '<div class="num"' + (style ? ' style="' + style + '"' : '') + '>' + Ui.escapeHtml(value) + '</div>' +
            '<div class="sub">' + Ui.escapeHtml(sub) + '</div></div></div>';
    }

    function statCards(d) {
        var checkIn = d.checkInMatchCount > 0
            ? statCard('Tiến độ điểm danh', d.checkedInTeamCount + '/' + d.expectedTeamCount, d.checkInMatchCount + ' trận đang mở điểm danh')
            : statCard('Tiến độ điểm danh', '—', 'Không có trận nào đang mở điểm danh');

        var fee = d.hasEntryFee
            ? statCard('Lệ phí đã xác nhận', formatMoney(d.confirmedFeeTotal),
                d.confirmedPaymentCount + ' đội, theo số đội khai' + (d.missingAmountCount > 0 ? ', ' + d.missingAmountCount + ' đội không khai số tiền' : ''),
                'font-size:1.5rem;margin-top:14px')
            : statCard('Lệ phí', 'Miễn phí', 'Giải không thu lệ phí', 'font-size:1.5rem;margin-top:14px');

        return '<div class="row row-cols-1 row-cols-sm-2 row-cols-lg-3 g-3">' +
            statCard('Đơn chờ duyệt', String(d.pendingCount), 'cần Ban tổ chức xử lý', d.pendingCount > 0 ? 'color:var(--warn)' : '') +
            statCard('Đội đã duyệt', d.approvedCount + '/' + d.maxTeams, 'còn ' + d.remainingSlots + ' suất') +
            statCard('Hàng đợi', String(d.waitlistedCount), 'đội trong danh sách chờ') +
            checkIn +
            statCard('Tranh chấp đang mở', String(d.openDisputeCount),
                d.pendingAppealCount > 0 ? d.pendingAppealCount + ' khiếu nại đang chờ' : 'không có khiếu nại đang chờ',
                d.openDisputeCount > 0 ? 'color:var(--danger)' : '') +
            fee +
            '</div>';
    }

    function countBadges(items, emptyText) {
        if (!items.length) return '<div style="color:var(--muted);font-size:.9rem">' + Ui.escapeHtml(emptyText) + '</div>';
        return items.map(function (c) {
            return '<span class="badge badge-soft" style="margin:0 6px 6px 0">' + Ui.escapeHtml(c.label) + ': ' + c.count + '</span>';
        }).join('');
    }

    function section(title, bodyHtml) {
        return '<div class="col-12 col-lg-6"><div class="card-etoms h-100"><div class="body">' +
            '<div class="section-title" style="margin:0 0 14px">' + Ui.escapeHtml(title) + '</div>' + bodyHtml + '</div></div></div>';
    }

    function listOrEmpty(items, rowFn, emptyText) {
        if (!items.length) return '<div style="color:var(--muted);font-size:.9rem">' + Ui.escapeHtml(emptyText) + '</div>';
        return items.map(rowFn).join('');
    }

    function row(name, detail, badgeHtml) {
        return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:10px;margin-bottom:10px">' +
            '<div class="grow"><div class="nm">' + Ui.escapeHtml(name) + '</div><div class="rl">' + Ui.escapeHtml(detail) + '</div></div>' +
            (badgeHtml || '') + '</div>';
    }

    function render(d) {
        document.getElementById('page-title').textContent = 'Bảng điều hành ' + d.tournamentName;
        document.getElementById('page-sub').innerHTML =
            'Trạng thái giải: <span class="badge ' + Ui.escapeHtml(d.statusClass) + '">' + Ui.escapeHtml(d.statusLabel) + '</span>';

        var todayRows = listOrEmpty(d.todayMatches, function (m) {
            return row(m.teamAName + ' vs ' + m.teamBName, formatTime(m.scheduledAt),
                '<span class="badge ' + Ui.escapeHtml(m.statusClass) + '">' + Ui.escapeHtml(m.statusLabel) + '</span>');
        }, 'Hôm nay không có trận nào.');

        var pendingRows = listOrEmpty(d.pendingRegistrations, function (r) {
            return row(r.teamName, r.submittedAt ? 'Nộp lúc ' + formatTime(r.submittedAt) : 'Chưa rõ thời điểm nộp');
        }, 'Không có đơn nào đang chờ duyệt.');

        var disputeRows = listOrEmpty(d.openDisputes, function (x) {
            return row(x.teamAName + ' vs ' + x.teamBName, x.reasonLabel + ' · ' + formatTime(x.openedAt),
                x.hasPendingAppeal ? '<span class="badge badge-gold">Có khiếu nại</span>' : '');
        }, 'Không có tranh chấp nào đang mở.');

        var overview =
            '<div class="fw-bold mb-2">Đơn đăng ký</div>' + countBadges(d.registrationsByStatus, 'Chưa có đơn đăng ký.') +
            '<div style="font-weight:700;margin:14px 0 8px">Trận đấu (' + d.totalMatches + ')</div>' + countBadges(d.matchesByStatus, 'Chưa có trận đấu.');

        document.getElementById('dashboard-root').innerHTML =
            statCards(d) +
            '<div class="row g-3 mt-2">' +
            section('Trận đấu hôm nay', todayRows) +
            section('Đơn chờ duyệt', pendingRows) +
            section('Tranh chấp đang mở', disputeRows) +
            section('Tổng quan', overview) +
            '</div>';   // các lối tắt (quản lý, thể lệ, trọng tài…) đã có ở thanh bên — không lặp lại ở đây
    }

    // ===== Mục 28: Tiến trình giải (bắt đầu / kết thúc theo việc thật, chưa đủ đội, gia hạn đăng ký) =====
    var tieOrder = [];

    function localInput(iso) {
        if (!iso) return '';
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        var p = function (n) { return String(n).padStart(2, '0'); };
        return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) + 'T' + p(d.getHours()) + ':' + p(d.getMinutes());
    }

    function lifecycleSteps(l) {
        var done = { reg: !l.registrationOpen && l.status !== 'DRAFT', bracket: l.bracketLocked, live: l.status === 'ONGOING' || l.status === 'COMPLETED', end: l.status === 'COMPLETED' };
        var steps = [
            ['Đăng ký', done.reg, l.approvedTeams + '/' + l.maxTeams + ' đội'],
            ['Sinh & khóa nhánh', done.bracket, l.hasBracket ? (l.bracketLocked ? 'Đã khóa' : 'Bản nháp') : 'Chưa sinh'],
            ['Đang diễn ra', done.live, l.matchesTotal ? l.matchesFinished + '/' + l.matchesTotal + ' trận' : ''],
            ['Kết thúc', done.end, '']
        ];
        var current = steps.findIndex(function (s) { return !s[1]; });
        return '<ol class="lc-steps">' + steps.map(function (s, i) {
            var cls = s[1] ? 'done' : i === current ? 'now' : '';
            return '<li class="' + cls + '"><span class="dot">' + (s[1] ? '✓' : i + 1) + '</span><b>' + Ui.escapeHtml(s[0]) + '</b>' +
                (s[2] ? '<small>' + Ui.escapeHtml(s[2]) + '</small>' : '') + '</li>';
        }).join('') + '</ol>';
    }

    function renderLifecycle(l) {
        var box = document.getElementById('lifecycle-root');
        if (!box) return;
        if (l.status === 'DRAFT' || l.status === 'CANCELLED') { box.innerHTML = ''; return; }
        var actions = '';
        if (l.canStart) actions += '<button type="button" class="btn btn-primary" id="lc-start">Bắt đầu giải</button>';
        if (l.canFinish) actions += '<button type="button" class="btn btn-primary" id="lc-finish">Kết thúc giải</button>';
        if (!l.hasBracket && !l.registrationOpen && !l.shortOfTeams && l.status === 'PUBLISHED' && Date.now() >= new Date(l.regOpenAt + 'Z').getTime())
            actions += '<a class="btn btn-primary" href="/bracket/manage.html?id=' + l.tournamentId + '">Sinh nhánh đấu</a>';
        if (l.hasBracket && !l.bracketLocked && l.status === 'PUBLISHED')
            actions += '<a class="btn btn-ghost" href="/bracket/manage.html?id=' + l.tournamentId + '">Kiểm tra & khóa nhánh</a>';
        if (l.shortOfTeams) actions += '<a class="btn btn-ghost" href="/tournament/cancel.html?id=' + l.tournamentId + '">Hủy giải</a>';

        var reason = '';
        if (l.status === 'ONGOING' && !l.canFinish && l.finishBlocked) reason = l.finishBlocked;

        var tie = '';
        if (l.canFinish && l.tiedTop && l.tiedTop.length) {
            if (tieOrder.length !== l.tiedTop.length) tieOrder = l.tiedTop.map(function (t) { return t.teamId; });
            var byId = {}; l.tiedTop.forEach(function (t) { byId[t.teamId] = t.name; });
            tie = '<div class="lc-tie"><div class="fw-bold mb-1">Chọn thứ tự cho các đội đang bằng nhau</div>' +
                '<p class="field-hint mb-2">Các đội đứng đầu bằng nhau mọi chỉ số (thường do bốc thăm hoặc trận phân định ngoài hệ thống). Đội ở trên xếp hạng cao hơn.</p><ol>' +
                tieOrder.map(function (id, i) {
                    return '<li><span>' + Ui.escapeHtml(byId[id] || '—') + '</span>' +
                        (i > 0 ? '<button type="button" class="btn btn-ghost btn-sm" data-tie-up="' + i + '" aria-label="Đưa ' + Ui.escapeHtml(byId[id] || '') + ' lên trên">Lên</button>' : '') + '</li>';
                }).join('') + '</ol></div>';
        }

        var extend = '';
        if (l.canExtend) {
            var soon = new Date(Date.now() + 3 * 86400000); soon.setMinutes(0, 0, 0);
            var start = new Date(soon.getTime() + 86400000);
            extend = '<form class="lc-extend" id="lc-extend" novalidate><div class="fw-bold mb-2">Gia hạn đăng ký</div><div class="row g-2 align-items-end">' +
                '<div class="col-12 col-md-4"><label class="form-label small" for="lc-close">Đóng đăng ký mới</label><input type="datetime-local" class="input" id="lc-close" value="' + localInput(soon.toISOString()) + '"></div>' +
                '<div class="col-12 col-md-4"><label class="form-label small" for="lc-start-at">Khai mạc mới</label><input type="datetime-local" class="input" id="lc-start-at" value="' + localInput(start.toISOString()) + '"></div>' +
                '<div class="col-12 col-md-4"><button type="submit" class="btn btn-ghost w-100">Gia hạn đăng ký</button></div></div>' +
                '<small class="field-hint d-block mt-1">Khai mạc phải sau đóng đăng ký ít nhất 1 giờ. Các đội đã đăng ký sẽ nhận thông báo.</small></form>';
        }

        var podium = '';
        if (l.podium && l.podium.length) {
            podium = '<div class="lc-podium">' + l.podium.map(function (p) {
                var medal = p.rank === 1 ? 'Vô địch' : p.rank === 2 ? 'Á quân' : 'Hạng Ba';
                return '<div><small>' + medal + '</small><b>' + Ui.escapeHtml(p.name) + '</b></div>';
            }).join('') + '</div>';
        }

        box.innerHTML = '<section class="card-etoms mb-3 lc-card"><div class="body">' +
            '<div class="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-2">' +
            '<div><div class="section-title" style="margin:0">Tiến trình giải</div>' +
            '<span class="badge ' + Ui.escapeHtml(l.labelClass) + '">' + Ui.escapeHtml(l.label) + '</span></div>' +
            (actions ? '<div class="d-flex flex-wrap gap-2">' + actions + '</div>' : '') + '</div>' +
            lifecycleSteps(l) +
            '<p class="lc-hint">' + Ui.escapeHtml(l.hint) + '</p>' +
            (reason ? '<p class="field-hint mb-0">' + Ui.escapeHtml(reason) + '</p>' : '') +
            tie + podium + extend +
            '</div></section>';

        var flash = document.getElementById('flash');
        var startBtn = document.getElementById('lc-start');
        if (startBtn) startBtn.addEventListener('click', async function () {
            var early = Date.now() < new Date(l.startAt + 'Z').getTime();
            var ok = await window.etomsConfirm((early ? 'Chưa tới giờ khai mạc (' + formatTime(l.startAt) + '). ' : '') +
                'Bắt đầu giải ngay? Các đội sẽ nhận thông báo giải đã bắt đầu.', { title: 'Bắt đầu giải', okText: 'Bắt đầu' });
            if (!ok) return;
            try { var r = await Api.post('/api/tournaments/' + tournamentId + '/lifecycle/start', {}); Ui.showAlert(flash, 'success', r.message); renderLifecycle(r.data); }
            catch (err) { Ui.showAlert(flash, 'danger', err.message); }
        });
        var finishBtn = document.getElementById('lc-finish');
        if (finishBtn) finishBtn.addEventListener('click', async function () {
            var ok = await window.etomsConfirm('Kết thúc giải và ghi hạng chung cuộc vào bảng xếp hạng? Sau khi kết thúc không sửa được kết quả của giải.', { title: 'Kết thúc giải', okText: 'Kết thúc' });
            if (!ok) return;
            try {
                var r = await Api.post('/api/tournaments/' + tournamentId + '/lifecycle/finish', { tieOrder: l.tiedTop && l.tiedTop.length ? tieOrder : null });
                Ui.showAlert(flash, 'success', r.message); renderLifecycle(r.data);
            } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
        });
        box.querySelectorAll('[data-tie-up]').forEach(function (b) {
            b.addEventListener('click', function () {
                var i = Number(b.dataset.tieUp);
                var x = tieOrder[i]; tieOrder[i] = tieOrder[i - 1]; tieOrder[i - 1] = x;
                renderLifecycle(l);
            });
        });
        var ext = document.getElementById('lc-extend');
        // Báo lỗi ngay khi chọn giờ: không chọn giờ đã qua, khai mạc sau đóng đăng ký ít nhất 1 giờ.
        var extTimeline = ext ? Ui.liveTimeline(['lc-close', 'lc-start-at'], function (v) {
            var er = {}, now = Date.now();
            if (!v['lc-close']) er['lc-close'] = 'Chọn hạn đóng đăng ký mới.';
            else if (v['lc-close'].getTime() <= now) er['lc-close'] = 'Không chọn thời gian đã qua.';
            if (!v['lc-start-at']) er['lc-start-at'] = 'Chọn thời gian khai mạc mới.';
            else if (v['lc-start-at'].getTime() <= now) er['lc-start-at'] = 'Không chọn thời gian đã qua.';
            else if (v['lc-close'] && v['lc-start-at'] - v['lc-close'] < 3600 * 1000) er['lc-start-at'] = 'Khai mạc phải sau đóng đăng ký ít nhất 1 giờ.';
            return er;
        }) : null;
        if (ext) ext.addEventListener('submit', async function (e) {
            e.preventDefault();
            var close = document.getElementById('lc-close').value, start = document.getElementById('lc-start-at').value;
            if (!extTimeline.check()) return Ui.showAlert(flash, 'danger', 'Mốc thời gian gia hạn chưa hợp lệ — xem thông báo dưới từng ô.');
            try {
                var r = await Api.post('/api/tournaments/' + tournamentId + '/lifecycle/extend',
                    { regCloseAt: new Date(close).toISOString(), startAt: new Date(start).toISOString() });
                Ui.showAlert(flash, 'success', r.message); renderLifecycle(r.data);
            } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
        });
    }

    async function loadLifecycle() {
        try { renderLifecycle(await Api.get('/api/tournaments/' + tournamentId + '/lifecycle')); }
        catch (err) { /* thẻ tiến trình lỗi không chặn phần còn lại của bảng điều hành */ }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        tournamentId = Ui.qsInt('id');
        if (tournamentId === null) {
            showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.');
            return;
        }

        Ui.consumeFlash(document.getElementById('flash'));
        loadLifecycle();
        try {
            render(await Api.get('/api/tournaments/' + tournamentId + '/dashboard'));
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể xem bảng điều hành này', 'Chỉ Ban tổ chức của giải được xem.');
            else showEmpty('Không tải được số liệu của giải', err.message);
        }
    });
})();
