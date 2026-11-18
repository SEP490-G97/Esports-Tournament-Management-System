(function () {
    var FORMAT_LABELS = {
        SINGLE_ELIMINATION: 'Loại trực tiếp',
        DOUBLE_ELIMINATION: 'Loại kép',
        ROUND_ROBIN: 'Vòng tròn',
        HYBRID: 'Vòng bảng rồi loại trực tiếp'
    };
    var FIELDS = ['Name', 'MinStarters', 'MaxStarters', 'MaxSubstitutes', 'DefaultFormat', 'AllowedEvidenceTypes', 'MaxEvidenceFileSizeMb'];
    var TEXT_FIELDS = ['Name', 'DefaultFormat', 'AllowedEvidenceTypes'];
    var DEFAULTS = { name: '', minStarters: 5, maxStarters: 5, maxSubstitutes: 2, defaultFormat: 'SINGLE_ELIMINATION', allowedEvidenceTypes: 'png,jpg,jpeg,mp4', maxEvidenceFileSizeMb: 10 };

    var games = [];
    var editing = null; // { game, ruleset } — ruleset null = đang tạo mới

    function flash() { return document.getElementById('flash'); }
    function camel(name) { return name.charAt(0).toLowerCase() + name.slice(1); }

    function showEmpty(title, message) {
        document.getElementById('rules-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    function rosterText(r) {
        var starters = r.minStarters === r.maxStarters ? String(r.minStarters) : r.minStarters + '–' + r.maxStarters;
        return starters + ' chính thức, tối đa ' + r.maxSubstitutes + ' dự bị';
    }

    function rulesetRow(game, r) {
        return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:10px;margin:0 0 10px 0">' +
            '<div class="grow"><div class="nm">' + Ui.escapeHtml(r.name) + '</div>' +
            '<div class="rl">' + Ui.escapeHtml(rosterText(r)) + ' · ' + Ui.escapeHtml(FORMAT_LABELS[r.defaultFormat] || r.defaultFormat) + '</div>' +
            '<div class="rl">File: ' + Ui.escapeHtml(r.allowedEvidenceTypes) + ' · tối đa ' + r.maxEvidenceFileSizeMb + ' MB</div></div>' +
            '<span class="badge ' + (r.isActive ? 'badge-active' : 'badge-danger') + '">' + (r.isActive ? 'Còn dùng' : 'Đã loại') + '</span>' +
            '<div class="d-flex flex-wrap gap-2">' +
            '<button type="button" class="btn btn-ghost" data-edit="' + r.gameRulesetId + '">Sửa</button>' +
            '<button type="button" class="btn btn-ghost" data-maps="' + r.gameRulesetId + '">Bản đồ</button>' +
            '<button type="button" class="btn btn-ghost" data-positions="' + r.gameRulesetId + '">Vị trí</button>' +
            '<button type="button" class="btn btn-ghost" data-toggle="' + r.gameRulesetId + '">' + (r.isActive ? 'Loại' : 'Dùng lại') + '</button>' +
            '</div></div>';
    }

    function renderList(rulesetError) {
        if (!games.length) {
            showEmpty('Chưa có tựa game nào', 'Thêm tựa game trong phần quản lý danh mục game.');
            return;
        }
        var banner = rulesetError ? '<div class="pending-banner">' + Ui.escapeHtml(rulesetError) + '</div>' : '';
        document.getElementById('rules-root').innerHTML = banner + games.map(function (g) {
            var list = g.rulesets.length
                ? g.rulesets.map(function (r) { return rulesetRow(g, r); }).join('')
                : '<div class="hint mb-2">Chưa có bộ luật nào.</div>';
            return '<div class="card-etoms mb-3"><div class="body">' +
                '<div class="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">' +
                '<div class="section-title m-0">' + Ui.escapeHtml(g.title) + ' <span class="text-muted-2 text-small">[' + Ui.escapeHtml(g.shortCode) + ']</span>' +
                (g.isActive ? '' : ' <span class="badge badge-danger">Game ngừng hoạt động</span>') + '</div>' +
                (rulesetError ? '' : '<button type="button" class="btn btn-primary" data-add="' + g.gameId + '">+ Thêm bộ luật</button>') +
                '</div>' + list + '</div></div>';
        }).join('');

        wireList();
    }

    function findRuleset(id) {
        for (var i = 0; i < games.length; i++) {
            var r = games[i].rulesets.find(function (x) { return x.gameRulesetId === id; });
            if (r) return { game: games[i], ruleset: r };
        }
        return null;
    }

    function wireList() {
        document.querySelectorAll('[data-add]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var game = games.find(function (g) { return g.gameId === parseInt(btn.dataset.add, 10); });
                if (game) openEditor(game, null);
            });
        });
        document.querySelectorAll('[data-edit]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var found = findRuleset(parseInt(btn.dataset.edit, 10));
                if (found) openEditor(found.game, found.ruleset);
            });
        });
        document.querySelectorAll('[data-toggle]').forEach(function (btn) {
            btn.addEventListener('click', function () { toggle(parseInt(btn.dataset.toggle, 10)); });
        });
        document.querySelectorAll('[data-maps]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var found = findRuleset(parseInt(btn.dataset.maps, 10));
                if (found) GameMaps.open(owner(found));
            });
        });
        document.querySelectorAll('[data-positions]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var found = findRuleset(parseInt(btn.dataset.positions, 10));
                if (found) GamePositions.open(owner(found));
            });
        });
    }

    // Thông tin bộ luật truyền cho khung bản đồ và vị trí.
    function owner(found) {
        return { rulesetId: found.ruleset.gameRulesetId, title: found.game.title + ' · ' + found.ruleset.name };
    }

    function openEditor(game, ruleset) {
        editing = { game: game, ruleset: ruleset };
        var form = document.getElementById('rule-form');
        Ui.clearFieldErrors(form);
        Ui.clearAlert(flash());

        var source = ruleset || DEFAULTS;
        FIELDS.forEach(function (f) { document.getElementById(f).value = source[camel(f)]; });
        document.getElementById('rule-title').textContent = ruleset
            ? 'Sửa bộ luật ' + ruleset.name + ' · ' + game.title
            : 'Thêm bộ luật cho ' + game.title;

        var warning = document.getElementById('rule-warning');
        var openCount = ruleset ? ruleset.openTournamentCount : 0;
        warning.hidden = openCount === 0;
        warning.textContent = openCount + ' giải chưa kết thúc đang dùng bộ luật này. Số người và thể thức của các giải đó giữ nguyên; bản đồ, vị trí và định dạng file minh chứng sẽ theo bộ luật sau khi lưu.';

        var card = document.getElementById('rule-card');
        card.hidden = false;
        card.scrollIntoView({ behavior: 'smooth', block: 'start' });
        document.getElementById('Name').focus();
    }

    function formBody(status) {
        var body = {};
        FIELDS.forEach(function (f) {
            var v = document.getElementById(f).value;
            body[camel(f)] = TEXT_FIELDS.indexOf(f) >= 0 ? v : parseInt(v, 10);
        });
        if (status) body.status = status;
        return body;
    }

    function rulesetBody(r, status) {
        return {
            name: r.name, minStarters: r.minStarters, maxStarters: r.maxStarters, maxSubstitutes: r.maxSubstitutes,
            defaultFormat: r.defaultFormat, allowedEvidenceTypes: r.allowedEvidenceTypes,
            maxEvidenceFileSizeMb: r.maxEvidenceFileSizeMb, status: status
        };
    }

    function upsert(gameId, saved) {
        var game = games.find(function (g) { return g.gameId === gameId; });
        var i = game.rulesets.findIndex(function (r) { return r.gameRulesetId === saved.gameRulesetId; });
        if (i >= 0) game.rulesets[i] = saved; else game.rulesets.push(saved);
        game.rulesets.sort(function (x, y) {
            if (x.isActive !== y.isActive) return x.isActive ? -1 : 1;
            return x.name.localeCompare(y.name, 'vi');
        });
    }

    async function toggle(id) {
        var found = findRuleset(id);
        if (!found) return;
        Ui.clearAlert(flash());
        try {
            var res = await Api.put('/api/game-rulesets/' + id, rulesetBody(found.ruleset, found.ruleset.isActive ? 'RETIRED' : 'ACTIVE'));
            upsert(found.game.gameId, res.data);
            renderList(null);
            Ui.showAlert(flash(), 'success', res.message);
        } catch (err) {
            Ui.showAlert(flash(), 'danger', err.message);
        }
    }

    function wireForm() {
        var form = document.getElementById('rule-form');
        var saveBtn = document.getElementById('save-rule');

        document.getElementById('cancel-rule').addEventListener('click', function () {
            document.getElementById('rule-card').hidden = true;
            editing = null;
        });

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            if (!editing) return;
            Ui.clearAlert(flash());
            Ui.clearFieldErrors(form);

            saveBtn.disabled = true;
            try {
                var res = editing.ruleset
                    ? await Api.put('/api/game-rulesets/' + editing.ruleset.gameRulesetId, formBody(null))
                    : await Api.post('/api/game-rules/' + editing.game.gameId + '/rulesets', formBody(null));
                upsert(editing.game.gameId, res.data);
                document.getElementById('rule-card').hidden = true;
                editing = null;
                renderList(null);
                Ui.showAlert(flash(), 'success', res.message);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash(), 'danger', Object.keys(err.fieldErrors || {}).length ? 'Vui lòng kiểm tra lại thông tin.' : err.message);
            } finally {
                saveBtn.disabled = false;
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        try {
            var board = await Api.get('/api/game-rules');
            games = board.games;
            renderList(board.rulesetError);
            wireForm();
        } catch (err) {
            if (err.status === 403) showEmpty('Bạn không có quyền vào trang này', 'Chỉ quản trị viên hệ thống được khai bộ luật thi đấu.');
            else showEmpty('Không tải được danh sách tựa game', err.message);
        }
    });
})();
