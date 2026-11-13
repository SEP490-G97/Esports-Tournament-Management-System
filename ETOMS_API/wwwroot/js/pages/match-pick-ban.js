(function () {
    var ACTION_LABELS = { BAN: 'Cấm', PICK: 'Chọn', DECIDER: 'Bản đồ còn lại' };
    var matchId = null;
    var board = null;
    var draft = [];   // các lượt đang soạn: { action, side, gameMapId }

    function flash() { return document.getElementById('flash'); }

    function showEmpty(title, message) {
        document.getElementById('pickban-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    function formatTime(value) {
        var date = new Date(/Z$/.test(value) ? value : value + 'Z');
        return date.toLocaleString('vi-VN');
    }

    function resultHtml(b) {
        if (!b.steps.length) {
            return '<div class="hint">Chưa có kết quả cấm chọn bản đồ.</div>';
        }
        var games = b.games.length
            ? '<div class="section-title" style="margin:0 0 10px;font-size:1rem">Bản đồ thi đấu</div>' +
              b.games.map(function (g) {
                  return '<div class="member-card" style="margin-bottom:8px"><div class="grow">' +
                      '<div class="nm">Ván ' + g.gameNo + ': ' + Ui.escapeHtml(g.mapName) + '</div>' +
                      '<div class="rl">' + Ui.escapeHtml(g.pickedBy) + '</div></div></div>';
              }).join('')
            : '';
        var steps = '<div class="section-title" style="margin:16px 0 10px;font-size:1rem">Thứ tự các lượt</div>' +
            '<ol id="step-list" style="margin:0;padding-left:22px">' + b.steps.map(function (s) {
                var who = s.teamName ? Ui.escapeHtml(s.teamName) + ' · ' : '';
                var cls = s.action === 'BAN' ? 'badge-danger' : 'badge-active';
                return '<li style="margin-bottom:6px">' + who +
                    '<span class="badge ' + cls + '">' + Ui.escapeHtml(s.actionLabel) + '</span> ' +
                    Ui.escapeHtml(s.mapName) + '</li>';
            }).join('') + '</ol>';
        var recorded = b.recordedAt
            ? '<div class="hint" style="margin-top:12px">Nhập bởi ' + Ui.escapeHtml(b.recordedByName || 'không rõ') + ' lúc ' + Ui.escapeHtml(formatTime(b.recordedAt)) + '</div>'
            : '';
        return games + steps + recorded;
    }

    function optionList(items, selected) {
        return items.map(function (it) {
            return '<option value="' + Ui.escapeHtml(String(it.value)) + '"' + (String(it.value) === String(selected) ? ' selected' : '') + '>' +
                Ui.escapeHtml(it.text) + '</option>';
        }).join('');
    }

    function editorRow(step, i, b) {
        var actions = [{ value: 'BAN', text: 'Cấm' }, { value: 'PICK', text: 'Chọn' }, { value: 'DECIDER', text: 'Bản đồ còn lại' }];
        var sides = [{ value: 'A', text: b.teamAName }, { value: 'B', text: b.teamBName }];
        var maps = [{ value: '', text: '— Chọn bản đồ —' }].concat(b.maps.map(function (m) {
            return { value: m.gameMapId, text: m.name + (m.isActive ? '' : ' (đã loại)') };
        }));
        return '<div class="member-card" style="flex-wrap:wrap;gap:8px;margin-bottom:8px" data-step="' + i + '">' +
            '<div class="nm" style="min-width:64px">Lượt ' + (i + 1) + '</div>' +
            '<select class="select filter-select" data-field="action" aria-label="Loại lượt ' + (i + 1) + '">' + optionList(actions, step.action) + '</select>' +
            '<select class="select filter-select" data-field="side" aria-label="Đội ở lượt ' + (i + 1) + '"' + (step.action === 'DECIDER' ? ' disabled' : '') + '>' + optionList(sides, step.side || 'A') + '</select>' +
            '<select class="select filter-select" data-field="map" aria-label="Bản đồ ở lượt ' + (i + 1) + '">' + optionList(maps, step.gameMapId || '') + '</select>' +
            '<button type="button" class="btn btn-ghost" data-remove="' + i + '">Bỏ lượt</button>' +
            '</div>';
    }

    function editorHtml(b) {
        var rows = draft.length
            ? draft.map(function (s, i) { return editorRow(s, i, b); }).join('')
            : '<div class="hint" style="margin-bottom:10px">Chưa có lượt nào. Bấm "Thêm lượt" để bắt đầu.</div>';
        return '<div class="card-etoms" style="margin-top:18px"><div class="body">' +
            '<div class="section-title" style="margin-top:0">Nhập kết quả cấm chọn</div>' +
            '<p class="hint" style="margin:6px 0 14px">Nhập theo đúng thứ tự hai đội đã cấm chọn. Bản đồ được chọn là bản đồ của từng ván theo thứ tự; "Bản đồ còn lại" chỉ đặt ở lượt cuối.</p>' +
            '<div id="editor-rows">' + rows + '</div>' +
            '<div class="btn-row">' +
            '<button type="button" class="btn btn-ghost" id="add-step">+ Thêm lượt</button>' +
            '<button type="button" class="btn btn-primary" id="save-pickban">Lưu kết quả</button>' +
            '</div></div></div>';
    }

    function render() {
        var b = board;
        document.getElementById('page-title').textContent = b.teamAName + ' vs ' + b.teamBName;
        document.getElementById('page-sub').textContent = b.tournamentName + ' · BO' + b.bestOf +
            (b.rulesetName ? ' · Bộ luật ' + b.rulesetName : '');

        var notice = b.blockedReason ? '<div class="pending-banner">' + Ui.escapeHtml(b.blockedReason) + '</div>' : '';
        document.getElementById('pickban-root').innerHTML =
            '<div class="card-etoms"><div class="body">' +
            '<div style="margin-bottom:14px">Trạng thái trận: <span class="badge ' + Ui.escapeHtml(b.statusClass) + '">' + Ui.escapeHtml(b.statusLabel) + '</span></div>' +
            notice + resultHtml(b) + '</div></div>' +
            (b.canEdit ? editorHtml(b) : '');

        if (b.canEdit) wireEditor();
    }

    function readDraft() {
        document.querySelectorAll('[data-step]').forEach(function (row) {
            var i = parseInt(row.dataset.step, 10);
            draft[i].action = row.querySelector('[data-field=action]').value;
            draft[i].side = row.querySelector('[data-field=side]').value;
            var map = row.querySelector('[data-field=map]').value;
            draft[i].gameMapId = map ? parseInt(map, 10) : 0;
        });
    }

    function wireEditor() {
        document.getElementById('add-step').addEventListener('click', function () {
            readDraft();
            // Mặc định đổi lượt giữa hai đội, cấm trước.
            var last = draft[draft.length - 1];
            draft.push({ action: 'BAN', side: last && last.side === 'A' ? 'B' : 'A', gameMapId: 0 });
            render();
        });
        document.querySelectorAll('[data-remove]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                readDraft();
                draft.splice(parseInt(btn.dataset.remove, 10), 1);
                render();
            });
        });
        document.querySelectorAll('[data-field=action]').forEach(function (sel) {
            sel.addEventListener('change', function () {
                sel.closest('[data-step]').querySelector('[data-field=side]').disabled = sel.value === 'DECIDER';
            });
        });
        document.getElementById('save-pickban').addEventListener('click', save);
    }

    async function save() {
        readDraft();
        Ui.clearAlert(flash());
        var btn = document.getElementById('save-pickban');
        btn.disabled = true;
        try {
            var res = await Api.put('/api/matches/' + matchId + '/pick-ban', {
                steps: draft.map(function (s) {
                    return { action: s.action, side: s.action === 'DECIDER' ? null : s.side, gameMapId: s.gameMapId };
                })
            });
            load(res.data);
            Ui.showAlert(flash(), 'success', res.message);
        } catch (err) {
            Ui.showAlert(flash(), 'danger', err.message);
            btn.disabled = false;
        }
    }

    function load(data) {
        board = data;
        draft = data.steps.map(function (s) {
            return { action: s.action, side: s.side || 'A', gameMapId: s.gameMapId };
        });
        render();
    }

    document.addEventListener('DOMContentLoaded', async function () {
        matchId = Ui.qsInt('id');
        if (matchId === null) {
            showEmpty('Không xác định được trận đấu', 'Đường dẫn thiếu mã trận.');
            return;
        }
        try {
            var res = await Api.get('/api/matches/' + matchId + '/pick-ban');
            load(res.data);
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy trận đấu', 'Trận không tồn tại.');
            else showEmpty('Không tải được kết quả cấm chọn', err.message);
        }
    });
})();
