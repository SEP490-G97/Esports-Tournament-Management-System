(function () {
    var tournamentId = null;

    function showEmpty(title, message) {
        document.getElementById('manage-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    function checkRow(c) {
        var fix = c.key === 'RULEBOOK' && !c.isOk
            ? '<a class="btn btn-ghost" href="/tournament/rulebook.html?id=' + tournamentId + '">Soạn thể lệ</a>'
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

    function render(s) {
        document.getElementById('page-title').textContent = 'Quản lý: ' + s.tournamentName;

        var checks = s.status === 'DRAFT'
            ? '<div class="section-title mt-0">Điều kiện xuất bản</div><div class="mt-3">' + s.checks.map(checkRow).join('') + '</div>'
            : '';
        var blocked = s.unpublishBlockedReason
            ? '<div class="pending-banner mt-3">' + Ui.escapeHtml(s.unpublishBlockedReason) + '</div>'
            : '';

        document.getElementById('manage-root').innerHTML =
            '<div class="card-etoms"><div class="body">' +
            '<div class="mb-3">Trạng thái: <span class="badge ' + Ui.escapeHtml(s.statusClass) + '" id="status-badge">' + Ui.escapeHtml(s.statusLabel) + '</span></div>' +
            checks + blocked +
            (actionsHtml(s) ? '<div class="btn-row">' + actionsHtml(s) + '</div>' : '') +
            '<p class="hint mt-3 mb-0">Các khu vực quản lý khác (đơn đăng ký, chia nhánh, lịch thi đấu, trọng tài, thể lệ...) nằm ở menu bên trái.</p>' +
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
