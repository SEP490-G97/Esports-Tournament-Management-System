// GRL-02: Kho bản đồ của một bộ luật, mở từ nút "Bản đồ" trên trang bộ luật thi đấu.
var GameMaps = (function () {
    var game = null;      // { rulesetId, title } — bộ luật đang mở
    var maps = [];
    var editingId = null; // null = đang thêm mới

    function base() { return '/api/game-rulesets/' + game.rulesetId + '/maps'; }
    function flash() { return document.getElementById('flash'); }

    function thumb(m) {
        var style = 'width:72px;height:44px;border-radius:8px;object-fit:cover;background:var(--panel-2);border:1px solid var(--border);flex:none';
        return m.imageUrl
            ? '<img src="' + Ui.escapeHtml(m.imageUrl) + '" alt="Ảnh ' + Ui.escapeHtml(m.name) + '" style="' + style + '" />'
            : '<span style="' + style + ';display:grid;place-items:center;color:var(--muted-2);font-size:.75rem">Chưa có ảnh</span>';
    }

    function renderList() {
        var list = document.getElementById('map-list');
        if (!maps.length) {
            list.innerHTML = '<div class="hint">Bộ luật này chưa có bản đồ nào.</div>';
            return;
        }
        // Cùng thứ tự với server: còn dùng trước, rồi theo tên.
        maps.sort(function (x, y) {
            if (x.isActive !== y.isActive) return x.isActive ? -1 : 1;
            return x.name.localeCompare(y.name, 'vi');
        });
        list.innerHTML = maps.map(function (m) {
            return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:12px;margin-bottom:10px">' +
                '<div class="d-flex align-items-center gap-3 grow">' + thumb(m) +
                '<div class="nm">' + Ui.escapeHtml(m.name) + '</div></div>' +
                '<span class="badge ' + (m.isActive ? 'badge-active' : 'badge-danger') + '">' + (m.isActive ? 'Còn dùng' : 'Đã loại') + '</span>' +
                '<div class="d-flex gap-2">' +
                '<button type="button" class="btn btn-ghost" data-map-edit="' + m.gameMapId + '">Sửa</button>' +
                '<button type="button" class="btn btn-ghost" data-map-toggle="' + m.gameMapId + '">' + (m.isActive ? 'Loại' : 'Dùng lại') + '</button>' +
                '</div></div>';
        }).join('');

        list.querySelectorAll('[data-map-edit]').forEach(function (btn) {
            btn.addEventListener('click', function () { startEdit(parseInt(btn.dataset.mapEdit, 10)); });
        });
        list.querySelectorAll('[data-map-toggle]').forEach(function (btn) {
            btn.addEventListener('click', function () { toggle(parseInt(btn.dataset.mapToggle, 10)); });
        });
    }

    function resetForm() {
        editingId = null;
        var form = document.getElementById('map-form');
        form.reset();
        Ui.clearFieldErrors(form);
        document.getElementById('map-form-title').textContent = 'Thêm bản đồ';
        document.getElementById('save-map').textContent = 'Thêm bản đồ';
        document.getElementById('reset-map').hidden = true;
    }

    function startEdit(mapId) {
        var m = maps.find(function (x) { return x.gameMapId === mapId; });
        if (!m) return;
        resetForm();
        editingId = mapId;
        document.getElementById('MapName').value = m.name;
        document.getElementById('map-form-title').textContent = 'Sửa bản đồ ' + m.name;
        document.getElementById('save-map').textContent = 'Lưu bản đồ';
        document.getElementById('reset-map').hidden = false;
        document.getElementById('MapName').focus();
    }

    function replace(saved) {
        var i = maps.findIndex(function (x) { return x.gameMapId === saved.gameMapId; });
        if (i >= 0) maps[i] = saved; else maps.push(saved);
        renderList();
    }

    async function toggle(mapId) {
        var m = maps.find(function (x) { return x.gameMapId === mapId; });
        if (!m) return;
        Ui.clearAlert(flash());
        var fd = new FormData();
        fd.append('Name', m.name);
        fd.append('Status', m.isActive ? 'RETIRED' : 'ACTIVE');
        try {
            var res = await Api.postForm(base() + '/' + mapId, fd);
            replace(res.data);
            Ui.showAlert(flash(), 'success', res.message);
        } catch (err) {
            Ui.showAlert(flash(), 'danger', err.message);
        }
    }

    function wireForm() {
        var form = document.getElementById('map-form');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            if (!game) return;
            Ui.clearAlert(flash());
            Ui.clearFieldErrors(form);

            var fd = new FormData();
            fd.append('Name', document.getElementById('MapName').value);
            var file = document.getElementById('MapImage').files[0];
            if (file) fd.append('image', file);
            if (editingId !== null) {
                var current = maps.find(function (x) { return x.gameMapId === editingId; });
                fd.append('Status', current ? current.status : 'ACTIVE');
            }

            var btn = document.getElementById('save-map');
            btn.disabled = true;
            try {
                var res = editingId === null
                    ? await Api.postForm(base(), fd)
                    : await Api.postForm(base() + '/' + editingId, fd);
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

        document.getElementById('reset-map').addEventListener('click', resetForm);
        document.getElementById('close-maps').addEventListener('click', function () {
            document.getElementById('map-card').hidden = true;
            game = null;
        });
    }

    async function open(g) {
        game = g;
        maps = [];
        resetForm();
        Ui.clearAlert(flash());
        document.getElementById('map-title').textContent = 'Kho bản đồ ' + g.title;
        document.getElementById('map-list').innerHTML = '<div class="hint">Đang tải...</div>';
        var card = document.getElementById('map-card');
        card.hidden = false;
        card.scrollIntoView({ behavior: 'smooth', block: 'start' });

        try {
            var res = await Api.get(base());
            maps = res.data;
            renderList();
        } catch (err) {
            document.getElementById('map-list').innerHTML = '';
            Ui.showAlert(flash(), 'danger', err.message);
        }
    }

    document.addEventListener('DOMContentLoaded', wireForm);

    return { open: open };
})();
