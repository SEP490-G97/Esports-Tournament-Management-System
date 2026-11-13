(function () {
    function groupByPreserveOrder(arr, keyFn) {
        var order = [], map = {};
        arr.forEach(function (item) {
            var k = keyFn(item);
            if (!map[k]) { map[k] = []; order.push(k); }
            map[k].push(item);
        });
        return order.map(function (k) { return { key: k, items: map[k] }; });
    }

    function formatLabel(fmt) {
        return fmt === 'ROUND_ROBIN' ? 'Thể thức vòng tròn' : fmt === 'DOUBLE_ELIMINATION' ? 'Loại kép' : 'Loại trực tiếp';
    }

    function render(tree) {
        var root = document.getElementById('bracket-root');

        if (!tree.hasBracket) {
            root.innerHTML =
                '<div class="eyebrow">Nhánh đấu · BRK-10</div>' +
                '<div class="page-head"><h1>' + Ui.escapeHtml(tree.tournamentName) + '</h1>' +
                '<p>' + formatLabel(tree.format) + (tree.isLocked ? '<span class="badge badge-active ms-2">Đã khóa nhánh</span>' : '') + '</p></div>' +
                '<div class="card-etoms"><div class="empty"><h3>Giải chưa sinh nhánh đấu</h3><p>Quay lại khi ban tổ chức đã sinh nhánh.</p></div></div>';
            return;
        }

        var knockout = tree.matches.filter(function (m) { return m.stageType === 'KNOCKOUT'; });
        var groupMatches = tree.matches.filter(function (m) { return m.stageType === 'GROUP'; })
            .slice().sort(function (a, b) {
                return (a.groupLabel || '').localeCompare(b.groupLabel || '') || a.roundNo - b.roundNo || a.positionInRound - b.positionInRound;
            });
        var groups = groupByPreserveOrder(groupMatches, function (m) { return m.roundLabel; });

        var groupsHtml = '';
        groups.forEach(function (g) {
            groupsHtml += '<div class="section-title">' + Ui.escapeHtml(g.key) + '</div><div class="row g-3">';
            g.items.forEach(function (m) {
                var resultLine = m.winnerTeamId != null ? ('Thắng: ' + Ui.escapeHtml(m.winnerTeamName)) : 'Chưa thi đấu';
                groupsHtml +=
                    '<div class="col-12 col-md-6"><div class="member-card" style="justify-content:space-between">' +
                    '<div class="grow"><div class="nm">' + Ui.escapeHtml(m.teamAName || 'Chờ xác định') + ' <span class="text-muted-2">vs</span> ' + Ui.escapeHtml(m.teamBName || 'Chờ xác định') + '</div>' +
                    '<div class="rl">' + resultLine + '</div></div>' +
                    (m.winnerTeamId != null ? '<span class="badge badge-gold">✓</span>' : '') +
                    '</div></div>';
            });
            groupsHtml += '</div>';
        });

        root.innerHTML =
            '<div class="eyebrow">Nhánh đấu · BRK-10</div>' +
            '<div class="page-head"><h1>' + Ui.escapeHtml(tree.tournamentName) + '</h1>' +
            '<p>' + formatLabel(tree.format) + (tree.isLocked ? '<span class="badge badge-active ms-2">Đã khóa nhánh</span>' : '') + '</p></div>' +
            '<div class="alert alert-info-etoms">Đây là sơ đồ tóm tắt. Muốn kéo, phóng to, lọc theo đội hoặc xem hành trình của đội, mở <a class="back-link" href="/tournament/bracket.html' + location.search + '">nhánh đấu tương tác</a>.</div>' +
            (knockout.length > 0 ? '<div id="knockout-tree"></div>' : '') +
            groupsHtml;

        if (knockout.length > 0) {
            renderBracketTree(document.getElementById('knockout-tree'), knockout);
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var id = Ui.qsInt('id');
        var root = document.getElementById('bracket-root');
        if (!id) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã giải</h3></div></div>'; return; }
        try {
            var tree = await Api.get('/api/tournaments/' + id + '/bracket');
            render(tree);
        } catch (err) {
            if (err.status === 404) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy giải đấu</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    });
})();
