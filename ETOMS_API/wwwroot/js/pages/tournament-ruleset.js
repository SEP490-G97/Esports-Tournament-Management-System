(function () {
    var FORMAT_LABELS = {
        SINGLE_ELIMINATION: 'Loại trực tiếp',
        DOUBLE_ELIMINATION: 'Loại kép',
        ROUND_ROBIN: 'Vòng tròn',
        HYBRID: 'Vòng bảng rồi loại trực tiếp'
    };
    var tournamentId = null;

    function showEmpty(title, message) {
        document.getElementById('ruleset-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    function optionRow(o, board) {
        var selected = o.gameRulesetId === board.selectedRulesetId;
        var starters = o.minStarters === o.maxStarters ? String(o.minStarters) : o.minStarters + '–' + o.maxStarters;
        var badge = selected
            ? '<span class="badge badge-active">Đang dùng</span>'
            : (o.isActive ? '' : '<span class="badge badge-danger">Đã loại</span>');
        var canPick = board.canEdit && !selected && o.isActive && o.fitsTournament;
        return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:10px;margin-bottom:10px' + (selected ? ';border-left-color:var(--success)' : '') + '">' +
            '<div class="grow"><div class="nm">' + Ui.escapeHtml(o.name) + '</div>' +
            '<div class="rl">' + Ui.escapeHtml(starters + ' chính thức, tối đa ' + o.maxSubstitutes + ' dự bị · ' + (FORMAT_LABELS[o.defaultFormat] || o.defaultFormat)) + '</div>' +
            (o.mismatchReason && !selected ? '<div class="rl text-danger-etoms">' + Ui.escapeHtml(o.mismatchReason) + '</div>' : '') +
            '</div>' + badge +
            (canPick ? '<button type="button" class="btn btn-primary" data-pick="' + o.gameRulesetId + '">Chọn bộ luật này</button>' : '') +
            '</div>';
    }

    function render(board) {
        document.getElementById('page-title').textContent = 'Bộ luật giải ' + board.tournamentName;
        document.getElementById('page-sub').textContent = board.gameTitle + ' · Giải đang đặt ' + board.starterCount +
            ' người chính thức, tối đa ' + board.maxSubstitutes + ' dự bị.';

        var notice = board.blockedReason
            ? '<div class="pending-banner">' + Ui.escapeHtml(board.blockedReason) + '</div>'
            : (board.selectedRulesetId ? '' : '<div class="pending-banner">Giải chưa chọn bộ luật.</div>');
        var list = board.options.length
            ? board.options.map(function (o) { return optionRow(o, board); }).join('')
            : '<div class="hint">Tựa game của giải chưa có bộ luật nào còn dùng. Liên hệ quản trị viên hệ thống.</div>';

        document.getElementById('ruleset-root').innerHTML =
            '<div class="card-etoms"><div class="body">' + notice + list +
            '<div class="btn-row"><a class="btn btn-ghost" href="/tournament/manage.html?id=' + tournamentId + '">Quản lý giải</a></div>' +
            '</div></div>';

        document.querySelectorAll('[data-pick]').forEach(function (btn) {
            btn.addEventListener('click', function () { pick(parseInt(btn.dataset.pick, 10)); });
        });
    }

    async function pick(rulesetId) {
        var flash = document.getElementById('flash');
        Ui.clearAlert(flash);
        try {
            var res = await Api.put('/api/tournaments/' + tournamentId + '/ruleset', { gameRulesetId: rulesetId });
            render(res.data);
            Ui.showAlert(flash, 'success', res.message);
        } catch (err) {
            Ui.showAlert(flash, 'danger', err.message);
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

        try {
            var res = await Api.get('/api/tournaments/' + tournamentId + '/ruleset');
            render(res.data);
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể chọn bộ luật cho giải này', 'Chỉ Ban tổ chức của giải được chọn bộ luật.');
            else showEmpty('Không tải được bộ luật của giải', err.message);
        }
    });
})();
