// ADM-03: quản lý tựa game và luật mặc định (Admin) — API /api/admin/games.
(function () {
    var el = function (id) { return document.getElementById(id); };
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var FORMAT = { SINGLE_ELIMINATION: 'Loại trực tiếp', DOUBLE_ELIMINATION: 'Loại kép', ROUND_ROBIN: 'Vòng tròn', HYBRID: 'Vòng bảng rồi loại trực tiếp' };
    var games = [], editingId = null;

    function roster(g) {
        var starters = g.minStarters === g.maxStarters ? g.minStarters : g.minStarters + '–' + g.maxStarters;
        return starters + ' chính thức + ' + g.maxSubstitutes + ' dự bị';
    }

    function render() {
        if (!games.length) { el('list').innerHTML = '<div class="empty"><h3>Chưa có game nào</h3></div>'; return; }
        el('list').innerHTML = '<div class="table-wrap"><table class="data-table"><thead><tr><th>Game</th><th>Đội hình</th><th>Thể thức mặc định</th><th>Minh chứng</th><th>Đang dùng</th><th>Trạng thái</th><th class="text-end">Thao tác</th></tr></thead><tbody>' +
            games.map(function (g) {
                var logo = Ui.safeUrl(g.logoUrl) ? '<img src="' + esc(Ui.safeUrl(g.logoUrl)) + '" alt="" width="32" height="32" style="border-radius:8px;object-fit:cover;margin-right:10px;vertical-align:middle">' : '';
                return '<tr><td>' + logo + '<b>' + esc(g.title) + '</b> <span class="badge badge-soft">' + esc(g.shortCode) + '</span></td>' +
                    '<td>' + esc(roster(g)) + '</td>' +
                    '<td>' + esc(FORMAT[g.defaultFormat] || g.defaultFormat) + '</td>' +
                    '<td><span class="field-hint">' + esc(g.allowedEvidenceTypes) + ' · tối đa ' + g.maxEvidenceFileSizeMb + ' MB</span></td>' +
                    '<td><span class="field-hint">' + g.teams + ' đội · ' + g.tournaments + ' giải · ' +
                    '<a href="/admin/game-rules.html">' + g.rulesets + ' bộ luật</a></span></td>' +
                    '<td><span class="badge ' + (g.status === 'ACTIVE' ? 'badge-active">Đang dùng' : 'badge-danger">Đã ngừng') + '</span></td>' +
                    '<td class="text-end text-nowrap"><button type="button" class="btn btn-ghost btn-sm" data-edit="' + g.gameId + '">Sửa</button> ' +
                    '<button type="button" class="btn btn-ghost btn-sm" data-toggle="' + g.gameId + '">' + (g.status === 'ACTIVE' ? 'Ngừng' : 'Mở lại') + '</button></td></tr>';
            }).join('') + '</tbody></table></div>';
    }

    async function load() {
        try { games = await Api.get('/api/admin/games'); render(); }
        catch (err) { el('list').innerHTML = '<div class="empty"><h3>Không tải được danh sách game</h3><p>' + esc(err.message) + '</p></div>'; }
    }

    function openEditor(g) {
        editingId = g ? g.gameId : null;
        el('editor-title').textContent = g ? 'Sửa game ' + g.title : 'Thêm game';
        el('g-title').value = g ? g.title : '';
        el('g-code').value = g ? g.shortCode : '';
        el('g-min').value = g ? g.minStarters : 5;
        el('g-max').value = g ? g.maxStarters : 5;
        el('g-sub').value = g ? g.maxSubstitutes : 2;
        el('g-format').value = g ? g.defaultFormat : 'SINGLE_ELIMINATION';
        el('g-ext').value = g ? g.allowedEvidenceTypes : 'png,jpg,jpeg,mp4';
        el('g-size').value = g ? g.maxEvidenceFileSizeMb : 10;
        el('g-logo').value = '';
        el('g-logolink').value = '';
        el('editor').hidden = false;
        el('editor').scrollIntoView({ block: 'start', behavior: 'smooth' });
        el('g-title').focus();
    }

    async function save(e) {
        e.preventDefault();
        var flash = el('flash');
        var min = Number(el('g-min').value), max = Number(el('g-max').value);
        if (el('g-title').value.trim().length < 2) return Ui.showAlert(flash, 'danger', 'Tên game từ 2 đến 150 ký tự.');
        if (!/^[A-Za-z0-9]{2,10}$/.test(el('g-code').value.trim())) return Ui.showAlert(flash, 'danger', 'Mã viết tắt 2–10 ký tự, chỉ chữ không dấu và số.');
        if (!(min >= 1) || max < min) return Ui.showAlert(flash, 'danger', 'Số người chính thức tối đa không được nhỏ hơn tối thiểu.');
        var file = el('g-logo').files[0];
        if (file && file.size > 5 * 1024 * 1024) return Ui.showAlert(flash, 'danger', 'Logo tối đa 5 MB.');
        var fd = new FormData();
        fd.append('Title', el('g-title').value.trim());
        fd.append('ShortCode', el('g-code').value.trim().toUpperCase());
        fd.append('MinStarters', String(min));
        fd.append('MaxStarters', String(max));
        fd.append('MaxSubstitutes', el('g-sub').value || '0');
        fd.append('DefaultFormat', el('g-format').value);
        fd.append('AllowedEvidenceTypes', el('g-ext').value.trim());
        fd.append('MaxEvidenceFileSizeMb', el('g-size').value || '10');
        if (file) fd.append('LogoFile', file);
        else if (el('g-logolink').value.trim()) fd.append('LogoLink', el('g-logolink').value.trim());
        try {
            var res = editingId ? await Api.putForm('/api/admin/games/' + editingId, fd) : await Api.postForm('/api/admin/games', fd);
            Ui.showAlert(flash, 'success', res.message || 'Đã lưu game.');
            el('editor').hidden = true;
            await load();
        } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
    }

    async function onListClick(e) {
        var t = e.target.closest('button[data-edit],button[data-toggle]');
        if (!t) return;
        if (t.dataset.edit) return openEditor(games.find(function (g) { return g.gameId === Number(t.dataset.edit); }));
        var g = games.find(function (x) { return x.gameId === Number(t.dataset.toggle); });
        if (g && g.status === 'ACTIVE' && !await window.etomsConfirm('Ngừng game ' + g.title + '? Không tạo được đội và giải mới cho game này; đội và giải đang có không bị ảnh hưởng.', { title: 'Ngừng game', okText: 'Ngừng' })) return;
        try { var r = await Api.post('/api/admin/games/' + t.dataset.toggle + '/toggle', {}); Ui.showAlert(el('flash'), 'success', r.message); await load(); }
        catch (err) { Ui.showAlert(el('flash'), 'danger', err.message); }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ADMIN']);
        if (!me) return;
        el('btn-new').addEventListener('click', function () { openEditor(null); });
        el('g-cancel').addEventListener('click', function () { el('editor').hidden = true; });
        el('game-form').addEventListener('submit', save);
        el('list').addEventListener('click', onListClick);
        load();
    });
})();
