(function () {
    var tournamentId = null;

    function showEmpty(title, message) {
        document.getElementById('manage-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    function checkRow(c) {
        var fix = c.key === 'RULEBOOK' && !c.isOk
            ? '<a class="btn btn-ghost" href="/tournament/edit.html?id=' + tournamentId + '#rules">Soạn thể lệ</a>'
            : '';
        return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:10px;margin-bottom:10px">' +
            '<div class="grow"><div class="nm">' + Ui.escapeHtml(c.label) + '</div>' +
            (c.isOk ? '' : '<div class="rl">' + Ui.escapeHtml(c.message) + '</div>') + '</div>' +
            fix +
            '<span class="badge ' + (c.isOk ? 'badge-active' : 'badge-danger') + '">' + (c.isOk ? 'Đạt' : 'Thiếu') + '</span>' +
            '</div>';
    }

    function actionsHtml(s) {
        if (s.status === 'DRAFT') {
            return '<button type="button" class="btn btn-primary" id="publish-btn"' + (s.canPublish ? '' : ' disabled') + '>Xuất bản giải</button>';
        }
        if (s.status === 'PUBLISHED') {
            return '<button type="button" class="btn btn-ghost" id="unpublish-btn"' + (s.canUnpublish ? '' : ' disabled') + '>Gỡ xuất bản</button>';
        }
        return '';
    }

    function fmt(iso) {
        if (!iso) return '—';
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }
    function ms(iso) { return iso ? new Date(/Z$/.test(iso) ? iso : iso + 'Z').getTime() : null; }

    // Mục 19 (17/11): 5 giai đoạn của giải, mỗi giai đoạn: đã xong / đang ở / chưa tới, kèm số liệu và việc cần làm.
    function phases(s) {
        var now = Date.now(), id = tournamentId;
        var published = s.status !== 'DRAFT' && s.status !== 'CANCELLED';
        var regClosed = now >= ms(s.regCloseAt);
        var link = function (href, text) { return '<a class="btn btn-ghost btn-sm" href="' + href + '">' + text + '</a>'; };
        var list = [
            { key: 'draft', title: 'Soạn & xuất bản', done: published || s.status === 'COMPLETED',
              info: s.status === 'DRAFT' ? 'Giải đang là bản nháp, chỉ Ban tổ chức xem được.' : 'Đã xuất bản, mọi người xem được trang giải.',
              actions: s.status === 'DRAFT' ? link('/tournament/edit.html?id=' + id, 'Sửa thông tin & thể lệ') : '' },
            { key: 'reg', title: 'Nhận đăng ký', done: published && regClosed,
              info: 'Từ ' + fmt(s.regOpenAt) + ' đến ' + fmt(s.regCloseAt) + ' · Đã duyệt ' + s.approvedCount + '/' + s.maxTeams + ' đội' +
                    (s.pendingCount ? ' · ' + s.pendingCount + ' đơn chờ duyệt' : ''),
              actions: link('/registration/review.html?tournamentId=' + id, s.pendingCount ? 'Duyệt ' + s.pendingCount + ' đơn' : 'Đơn đăng ký') },
            { key: 'bracket', title: 'Chia & khóa nhánh', done: s.bracketLocked,
              info: s.bracketLocked ? 'Nhánh đấu đã khóa.' : s.bracketGenerated ? 'Đã sinh nhánh, chưa khóa — khóa để bắt đầu xếp lịch.' : (regClosed ? 'Chưa sinh nhánh đấu.' : 'Sinh nhánh sau khi đóng đăng ký.'),
              actions: link('/bracket/manage.html?id=' + id, 'Chia nhánh') },
            { key: 'play', title: 'Thi đấu', done: s.status === 'COMPLETED',
              info: 'Khai mạc ' + fmt(s.startAt) + (s.matchTotal ? ' · Đã xong ' + s.matchDone + '/' + s.matchTotal + ' trận' : '') +
                    (s.openDisputes ? ' · ' + s.openDisputes + ' tranh chấp đang mở' : ''),
              actions: link('/tournament/schedule.html?id=' + id, 'Lịch thi đấu') + link('/tournament/referees.html?id=' + id, 'Trọng tài') +
                       (s.openDisputes ? link('/result/disputes.html', 'Xử lý tranh chấp') : '') },
            { key: 'end', title: 'Kết thúc', done: s.status === 'COMPLETED',
              info: s.endAt ? 'Dự kiến kết thúc ' + fmt(s.endAt) + '.' : 'Chưa đặt ngày kết thúc.', actions: '' }
        ];
        // Giai đoạn hiện tại = giai đoạn đầu tiên chưa xong (giải hủy thì không có).
        var current = s.status === 'CANCELLED' ? -1 : list.findIndex(function (p) { return !p.done; });
        return { list: list, current: current };
    }

    function stepperHtml(s) {
        var p = phases(s);
        var steps = '<ol class="phase-steps">' + p.list.map(function (ph, i) {
            var cls = ph.done ? 'done' : i === p.current ? 'current' : '';
            return '<li class="' + cls + '"><span class="no">' + (ph.done ? '✓' : i + 1) + '</span><span class="t">' + Ui.escapeHtml(ph.title) + '</span></li>';
        }).join('') + '</ol>';
        var cur = p.current >= 0 ? p.list[p.current] : null;
        var detail = cur
            ? '<div class="phase-now"><div class="phase-now-head">Đang ở: <b>' + Ui.escapeHtml(cur.title) + '</b></div>' +
              '<p class="mb-2">' + Ui.escapeHtml(cur.info) + '</p>' + (cur.actions ? '<div class="btn-row mb-0">' + cur.actions + '</div>' : '') + '</div>'
            : '';
        // Các giai đoạn khác: thu gọn một dòng.
        var others = p.list.map(function (ph, i) {
            if (i === p.current) return '';
            return '<div class="phase-row' + (ph.done ? ' done' : '') + '"><span>' + Ui.escapeHtml(ph.title) + '</span><span class="hint">' + Ui.escapeHtml(ph.info) + '</span></div>';
        }).join('');
        return steps + detail + '<div class="phase-others">' + others + '</div>';
    }

    function render(s) {
        document.getElementById('page-title').textContent = 'Quản lý: ' + s.tournamentName;

        var checks = s.status === 'DRAFT'
            ? '<div class="section-title">Điều kiện xuất bản</div><div class="mt-3">' + s.checks.map(checkRow).join('') + '</div>'
            : '';
        var blocked = s.unpublishBlockedReason
            ? '<div class="pending-banner mt-3">' + Ui.escapeHtml(s.unpublishBlockedReason) + '</div>'
            : '';

        document.getElementById('manage-root').innerHTML =
            '<div class="card-etoms"><div class="body">' +
            '<div class="d-flex align-items-center gap-2 mb-3">Trạng thái: <span class="badge ' + Ui.escapeHtml(s.statusClass) + '" id="status-badge">' + Ui.escapeHtml(s.statusLabel) + '</span></div>' +
            (s.status === 'CANCELLED' ? '' : stepperHtml(s)) +
            checks + blocked +
            (actionsHtml(s) ? '<div class="btn-row mt-3">' + actionsHtml(s) + '</div>' : '') +
            '</div></div>' +
            // Khu vực cảnh báo: thao tác không hoàn tác (TOUR-11).
            '<div class="card-etoms" id="danger-zone" style="margin-top:16px;border-color:var(--danger)"><div class="body">' +
            '<div class="section-title mt-0 text-danger-etoms">Khu vực cảnh báo</div>' +
            '<p class="hint" style="margin:6px 0 12px">' + (s.status === 'CANCELLED'
                ? 'Giải đã bị hủy. Xem lý do và danh sách đội cần hoàn lệ phí.'
                : 'Hủy giải không hoàn tác được, cần lý do và được báo tới mọi đội trưởng. Không hủy được sau khi nhánh đấu đã khóa.') + '</p>' +
            '<a class="btn btn-ghost" href="/tournament/cancel.html?id=' + tournamentId + '">' + (s.status === 'CANCELLED' ? 'Xem việc hủy giải' : 'Hủy giải…') + '</a>' +
            '</div></div>';

        wireAction('publish-btn', 'Xuất bản giải đấu? Sau khi xuất bản, mọi người đều xem được trang giải.', 'publish');
        wireAction('unpublish-btn', 'Gỡ xuất bản? Giải sẽ trở về nháp và không còn hiện trên cổng công khai.', 'unpublish');
    }

    function wireAction(buttonId, confirmMessage, action) {
        var btn = document.getElementById(buttonId);
        if (!btn) return;
        btn.addEventListener('click', async function () {
            var flash = document.getElementById('flash');
            Ui.clearAlert(flash);
            if (!await window.etomsConfirm(confirmMessage)) return;

            btn.disabled = true;
            try {
                var res = await Api.post('/api/tournaments/' + tournamentId + '/' + action);
                await load();
                Ui.showAlert(flash, 'success', res.message);
            } catch (err) {
                Ui.showAlert(flash, 'danger', err.message);
                await load();
            }
        });
    }

    async function load() {
        try {
            render(await Api.get('/api/tournaments/' + tournamentId + '/publish-status'));
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể quản lý giải này', 'Chỉ Ban tổ chức của giải được xuất bản hoặc gỡ xuất bản.');
            else showEmpty('Không tải được thông tin giải', err.message);
        }
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
        await load();
    });
})();
