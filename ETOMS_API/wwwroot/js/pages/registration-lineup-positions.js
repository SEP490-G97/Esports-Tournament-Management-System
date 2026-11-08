(function () {
    var registrationId = null;

    function showEmpty(title, message) {
        document.getElementById('lineup-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    function selectHtml(member, board) {
        var options = '<option value="">— Chưa chọn —</option>' + board.positions.map(function (p) {
            var label = p.isActive ? p.name : p.name + ' (đã loại)';
            return '<option value="' + p.gamePositionId + '"' + (p.gamePositionId === member.gamePositionId ? ' selected' : '') + '>' + Ui.escapeHtml(label) + '</option>';
        }).join('');
        return '<select class="select" style="max-width:260px" data-lineup="' + member.registrationLineupId + '"' + (board.canEdit ? '' : ' disabled') + '>' + options + '</select>';
    }

    function render(board) {
        document.getElementById('page-title').textContent = 'Đội hình ' + board.teamName;
        document.getElementById('page-sub').textContent = board.tournamentName + ' · ' + board.gameTitle + ' · Đơn ' + board.registrationStatusLabel.toLowerCase();

        if (!board.members.length) {
            showEmpty('Đơn chưa có đội hình', 'Hãy khai đội hình trong đơn đăng ký trước.');
            return;
        }

        var rule = board.starterPositionRequired
            ? 'Mỗi người chính thức phải có một vị trí và không trùng nhau. Người dự bị có thể để trống.'
            : 'Hai người chính thức không được trùng vị trí. Có thể để trống.';
        var blocked = board.blockedReason
            ? '<div class="pending-banner">' + Ui.escapeHtml(board.blockedReason) + '</div>'
            : '<div class="hint" style="margin-bottom:14px">' + Ui.escapeHtml(rule) + '</div>';

        var rows = board.members.map(function (m) {
            return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:10px;margin-bottom:10px">' +
                '<div class="grow"><div class="nm">' + Ui.escapeHtml(m.displayName) + '</div>' +
                '<div class="rl">' + Ui.escapeHtml(m.slotLabel) + '</div></div>' +
                (board.positions.length ? selectHtml(m, board) : '') +
                '</div>';
        }).join('');

        document.getElementById('lineup-root').innerHTML =
            '<div class="card-etoms"><div class="body">' + blocked + rows +
            (board.canEdit ? '<div class="btn-row"><button type="button" class="btn btn-primary" id="save-lineup">Lưu vị trí</button></div>' : '') +
            '</div></div>';

        var saveBtn = document.getElementById('save-lineup');
        if (saveBtn) saveBtn.addEventListener('click', save);
    }

    async function save() {
        var flash = document.getElementById('flash');
        Ui.clearAlert(flash);
        var items = Array.prototype.map.call(document.querySelectorAll('[data-lineup]'), function (sel) {
            return {
                registrationLineupId: parseInt(sel.dataset.lineup, 10),
                gamePositionId: sel.value ? parseInt(sel.value, 10) : null
            };
        });

        var btn = document.getElementById('save-lineup');
        btn.disabled = true;
        try {
            var res = await Api.put('/api/registrations/' + registrationId + '/lineup-positions', { items: items });
            render(res.data);
            Ui.showAlert(flash, 'success', res.message);
        } catch (err) {
            Ui.showAlert(flash, 'danger', err.message);
            btn.disabled = false;
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        registrationId = Ui.qsInt('id');
        if (registrationId === null) {
            showEmpty('Không xác định được đơn đăng ký', 'Đường dẫn thiếu mã đơn.');
            return;
        }

        try {
            var res = await Api.get('/api/registrations/' + registrationId + '/lineup-positions');
            render(res.data);
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy đơn đăng ký', 'Đơn không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể sửa đội hình này', 'Chỉ đội trưởng của đội được chọn vị trí.');
            else showEmpty('Không tải được đội hình', err.message);
        }
    });
})();
