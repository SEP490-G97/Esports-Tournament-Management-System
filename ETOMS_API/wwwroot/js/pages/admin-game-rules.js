(function () {
    var FORMAT_LABELS = {
        SINGLE_ELIMINATION: 'Loại trực tiếp',
        DOUBLE_ELIMINATION: 'Loại kép',
        ROUND_ROBIN: 'Vòng tròn',
        HYBRID: 'Vòng bảng rồi loại trực tiếp'
    };
    var FIELDS = ['MinStarters', 'MaxStarters', 'MaxSubstitutes', 'DefaultFormat', 'AllowedEvidenceTypes', 'MaxEvidenceFileSizeMb'];

    var rules = [];
    var editing = null;

    function showEmpty(title, message) {
        document.getElementById('rules-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    function camel(name) { return name.charAt(0).toLowerCase() + name.slice(1); }

    function rosterText(r) {
        var starters = r.minStarters === r.maxStarters ? String(r.minStarters) : r.minStarters + '–' + r.maxStarters;
        return starters + ' chính thức, tối đa ' + r.maxSubstitutes + ' dự bị';
    }

    function renderList() {
        if (!rules.length) {
            showEmpty('Chưa có tựa game nào', 'Thêm tựa game trong phần quản lý danh mục game.');
            return;
        }
        document.getElementById('rules-root').innerHTML = rules.map(function (r) {
            return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:10px;margin-bottom:10px">' +
                '<div class="grow"><div class="nm">' + Ui.escapeHtml(r.title) + ' <span style="color:var(--muted-2)">[' + Ui.escapeHtml(r.shortCode) + ']</span></div>' +
                '<div class="rl">' + Ui.escapeHtml(rosterText(r)) + ' · ' + Ui.escapeHtml(FORMAT_LABELS[r.defaultFormat] || r.defaultFormat) + '</div>' +
                '<div class="rl">File: ' + Ui.escapeHtml(r.allowedEvidenceTypes) + ' · tối đa ' + r.maxEvidenceFileSizeMb + ' MB</div></div>' +
                (r.isActive ? '' : '<span class="badge badge-danger">Ngừng hoạt động</span>') +
                '<div class="d-flex gap-2">' +
                '<button type="button" class="btn btn-ghost" data-edit="' + r.gameId + '">Sửa luật</button>' +
                '<button type="button" class="btn btn-ghost" data-maps="' + r.gameId + '">Bản đồ</button>' +
                '<button type="button" class="btn btn-ghost" data-positions="' + r.gameId + '">Vị trí</button>' +
                '</div></div>';
        }).join('');

        document.querySelectorAll('[data-edit]').forEach(function (btn) {
            btn.addEventListener('click', function () { openEditor(parseInt(btn.dataset.edit, 10)); });
        });
        document.querySelectorAll('[data-maps]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var game = rules.find(function (r) { return r.gameId === parseInt(btn.dataset.maps, 10); });
                if (game) GameMaps.open(game);
            });
        });
        document.querySelectorAll('[data-positions]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var game = rules.find(function (r) { return r.gameId === parseInt(btn.dataset.positions, 10); });
                if (game) GamePositions.open(game);
            });
        });
    }

    function openEditor(gameId) {
        editing = rules.find(function (r) { return r.gameId === gameId; });
        if (!editing) return;

        var form = document.getElementById('rule-form');
        Ui.clearFieldErrors(form);
        Ui.clearAlert(document.getElementById('flash'));
        document.getElementById('rule-title').textContent = 'Sửa luật ' + editing.title;
        FIELDS.forEach(function (f) { document.getElementById(f).value = editing[camel(f)]; });

        var warning = document.getElementById('rule-warning');
        warning.hidden = editing.openTournamentCount === 0;
        warning.textContent = editing.openTournamentCount + ' giải chưa kết thúc đang dùng tựa game này. Luật mới chỉ áp dụng cho giải tạo sau này.';

        var card = document.getElementById('rule-card');
        card.hidden = false;
        card.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function wireForm() {
        var form = document.getElementById('rule-form');
        var flash = document.getElementById('flash');
        var saveBtn = document.getElementById('save-rule');

        document.getElementById('cancel-rule').addEventListener('click', function () {
            document.getElementById('rule-card').hidden = true;
            editing = null;
        });

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            if (!editing) return;
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);

            var body = {};
            FIELDS.forEach(function (f) {
                var v = document.getElementById(f).value;
                body[camel(f)] = (f === 'DefaultFormat' || f === 'AllowedEvidenceTypes') ? v : parseInt(v, 10);
            });

            saveBtn.disabled = true;
            try {
                var res = await Api.put('/api/game-rules/' + editing.gameId, body);
                var i = rules.findIndex(function (r) { return r.gameId === res.data.gameId; });
                rules[i] = res.data;
                document.getElementById('rule-card').hidden = true;
                editing = null;
                renderList();
                Ui.showAlert(flash, 'success', res.message);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', Object.keys(err.fieldErrors || {}).length ? 'Vui lòng kiểm tra lại thông tin.' : err.message);
            } finally {
                saveBtn.disabled = false;
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        try {
            rules = await Api.get('/api/game-rules');
            renderList();
            wireForm();
        } catch (err) {
            if (err.status === 403) showEmpty('Bạn không có quyền vào trang này', 'Chỉ quản trị viên hệ thống được khai luật thi đấu.');
            else showEmpty('Không tải được danh sách tựa game', err.message);
        }
    });
})();
