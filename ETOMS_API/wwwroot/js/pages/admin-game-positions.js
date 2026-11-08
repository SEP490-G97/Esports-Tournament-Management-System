// GRL-03: Vị trí trong đội của một bộ luật, mở từ nút "Vị trí" trên trang bộ luật thi đấu.
var GamePositions = (function () {
    var game = null;      // { rulesetId, title } — bộ luật đang mở
    var positions = [];   // đã xếp theo sortOrder từ server
    var editingId = null; // null = đang thêm mới

    function base() { return '/api/game-rulesets/' + game.rulesetId + '/positions'; }
    function flash() { return document.getElementById('flash'); }

    function renderList() {
        var list = document.getElementById('position-list');
        if (!positions.length) {
            list.innerHTML = '<div class="hint">Bộ luật này chưa khai vị trí nào. Khi đó đội hình chỉ phân biệt chính thức và dự bị.</div>';
            return;
        }
        var last = positions.length - 1;
        list.innerHTML = positions.map(function (p, i) {
            return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:10px;margin-bottom:10px">' +
                '<div class="grow"><div class="nm">' + (i + 1) + '. ' + Ui.escapeHtml(p.name) + '</div></div>' +
                '<span class="badge ' + (p.isActive ? 'badge-active' : 'badge-danger') + '">' + (p.isActive ? 'Còn dùng' : 'Đã loại') + '</span>' +
                '<div class="d-flex gap-2">' +
                '<button type="button" class="btn btn-ghost" data-pos-move="up" data-pos-id="' + p.gamePositionId + '" title="Lên trên"' + (i === 0 ? ' disabled' : '') + '>↑</button>' +
                '<button type="button" class="btn btn-ghost" data-pos-move="down" data-pos-id="' + p.gamePositionId + '" title="Xuống dưới"' + (i === last ? ' disabled' : '') + '>↓</button>' +
                '<button type="button" class="btn btn-ghost" data-pos-edit="' + p.gamePositionId + '">Sửa</button>' +
                '<button type="button" class="btn btn-ghost" data-pos-toggle="' + p.gamePositionId + '">' + (p.isActive ? 'Loại' : 'Dùng lại') + '</button>' +
                '</div></div>';
        }).join('');

        list.querySelectorAll('[data-pos-move]').forEach(function (btn) {
            btn.addEventListener('click', function () { move(parseInt(btn.dataset.posId, 10), btn.dataset.posMove); });
        });
        list.querySelectorAll('[data-pos-edit]').forEach(function (btn) {
            btn.addEventListener('click', function () { startEdit(parseInt(btn.dataset.posEdit, 10)); });
        });
        list.querySelectorAll('[data-pos-toggle]').forEach(function (btn) {
            btn.addEventListener('click', function () { toggle(parseInt(btn.dataset.posToggle, 10)); });
        });
    }

    function find(id) { return positions.find(function (p) { return p.gamePositionId === id; }); }

    function replace(saved) {
        var i = positions.findIndex(function (p) { return p.gamePositionId === saved.gamePositionId; });
        if (i >= 0) positions[i] = saved; else positions.push(saved);
        renderList();
    }

    function resetForm() {
        editingId = null;
        var form = document.getElementById('position-form');
        form.reset();
        Ui.clearFieldErrors(form);
        document.getElementById('position-form-title').textContent = 'Thêm vị trí';
        document.getElementById('save-position').textContent = 'Thêm vị trí';
        document.getElementById('reset-position').hidden = true;
    }

    function startEdit(id) {
        var p = find(id);
        if (!p) return;
        resetForm();
        editingId = id;
        document.getElementById('PositionName').value = p.name;
        document.getElementById('position-form-title').textContent = 'Đổi tên vị trí ' + p.name;
        document.getElementById('save-position').textContent = 'Lưu vị trí';
        document.getElementById('reset-position').hidden = false;
        document.getElementById('PositionName').focus();
    }

    async function move(id, direction) {
        Ui.clearAlert(flash());
        try {
            var res = await Api.post(base() + '/' + id + '/move', { direction: direction });
            positions = res.data;
            renderList();
        } catch (err) {
            Ui.showAlert(flash(), 'danger', err.message);
        }
    }

    async function toggle(id) {
        var p = find(id);
        if (!p) return;
        Ui.clearAlert(flash());
        try {
            var res = await Api.put(base() + '/' + id, { name: p.name, status: p.isActive ? 'RETIRED' : 'ACTIVE' });
            replace(res.data);
            Ui.showAlert(flash(), 'success', res.message);
        } catch (err) {
            Ui.showAlert(flash(), 'danger', err.message);
        }
    }

    function wireForm() {
        var form = document.getElementById('position-form');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            if (!game) return;
            Ui.clearAlert(flash());
            Ui.clearFieldErrors(form);

            var name = document.getElementById('PositionName').value;
            var btn = document.getElementById('save-position');
            btn.disabled = true;
            try {
                var res = editingId === null
                    ? await Api.post(base(), { name: name })
                    : await Api.put(base() + '/' + editingId, { name: name });
                replace(res.data);
                resetForm();
                Ui.showAlert(flash(), 'success', res.message);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash(), 'danger', err.message);
            } finally {
                btn.disabled = false;
            }
        });

        document.getElementById('reset-position').addEventListener('click', resetForm);
        document.getElementById('close-positions').addEventListener('click', function () {
            document.getElementById('position-card').hidden = true;
            game = null;
        });
    }

    async function open(g) {
        game = g;
        positions = [];
        resetForm();
        Ui.clearAlert(flash());
        document.getElementById('position-title').textContent = 'Vị trí trong đội ' + g.title;
        document.getElementById('position-list').innerHTML = '<div class="hint">Đang tải...</div>';
        var card = document.getElementById('position-card');
        card.hidden = false;
        card.scrollIntoView({ behavior: 'smooth', block: 'start' });

        try {
            var res = await Api.get(base());
            positions = res.data;
            renderList();
        } catch (err) {
            document.getElementById('position-list').innerHTML = '';
            Ui.showAlert(flash(), 'danger', err.message);
        }
    }

    document.addEventListener('DOMContentLoaded', wireForm);

    return { open: open };
})();
