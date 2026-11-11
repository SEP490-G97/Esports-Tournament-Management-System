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
        document.getElementById('page-title').textContent = 'Quản lý giải ' + s.tournamentName;

        var checks = s.status === 'DRAFT'
            ? '<div class="section-title" style="margin-top:0">Điều kiện xuất bản</div><div style="margin-top:14px">' + s.checks.map(checkRow).join('') + '</div>'
            : '';
        var blocked = s.unpublishBlockedReason
            ? '<div class="pending-banner" style="margin-top:14px">' + Ui.escapeHtml(s.unpublishBlockedReason) + '</div>'
            : '';

        document.getElementById('manage-root').innerHTML =
            '<div class="card-etoms"><div class="body">' +
            '<div style="margin-bottom:18px">Trạng thái: <span class="badge ' + Ui.escapeHtml(s.statusClass) + '" id="status-badge">' + Ui.escapeHtml(s.statusLabel) + '</span></div>' +
            checks + blocked +
            '<div class="btn-row">' + actionsHtml(s) +
            '<a class="btn btn-ghost" href="/tournament/rulebook.html?id=' + tournamentId + '">Thể lệ giải</a>' +
            '<a class="btn btn-ghost" href="/tournament/ruleset.html?id=' + tournamentId + '">Bộ luật</a>' +
            '<a class="btn btn-ghost" href="/tournament/dashboard.html?id=' + tournamentId + '">Bảng điều hành</a>' +
            '<a class="btn btn-ghost" href="/tournament/clone.html?id=' + tournamentId + '">Nhân bản giải</a>' +
            '<a class="btn btn-ghost" href="/tournament/stage-transition.html?id=' + tournamentId + '">Chuyển sang knockout</a>' +
            '<a class="btn btn-ghost" href="/tournament/schedule.html?id=' + tournamentId + '">Xếp lịch</a>' +
            '<a class="btn btn-ghost" href="/tournament/standings.html?id=' + tournamentId + '">Bảng xếp hạng</a>' +
            '<a class="btn btn-ghost" href="/tournament/bracket.html?id=' + tournamentId + '">Nhánh đấu</a></div>' +
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
