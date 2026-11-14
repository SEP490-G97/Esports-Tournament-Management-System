(function () {
    function statusBadge(status) {
        return status === 'ACTIVE'
            ? '<span class="badge badge-active">Hoạt động</span>'
            : '<span class="badge" style="background:var(--danger-bg);color:var(--danger-ink)">Giải tán</span>';
    }

    function renderList(list, hasFilter) {
        var root = document.getElementById('result-root');
        if (!list.length) {
            if (hasFilter) {
                root.innerHTML = '<div class="card-etoms"><div class="empty">' +
                    '<h3>Không tìm thấy đội phù hợp</h3><p>Thử đổi từ khóa hoặc bộ lọc khác.</p>' +
                    '<a class="btn btn-ghost" href="/team/index.html">Xóa bộ lọc</a></div></div>';
            } else {
                root.innerHTML = '<div class="card-etoms"><div class="empty">' +
                    '<h3>Chưa có đội nào</h3><p>Hãy tạo đội đầu tiên để bắt đầu.</p>' +
                    '<a class="btn btn-primary" data-hide-roles="ORGANIZER ADMIN" href="/team/create.html">Tạo đội mới</a></div></div>';
            }
            return;
        }
        var html = '<div style="color:var(--muted);font-size:.86rem;margin-bottom:16px"><strong class="text-ink">' + list.length + '</strong> đội phù hợp</div>';
        html += '<div class="row g-3">';
        list.forEach(function (t) {
            var logo = t.logoUrl
                ? '<img class="team-logo" src="' + Ui.escapeHtml(t.logoUrl) + '" alt="Logo ' + Ui.escapeHtml(t.name) + '" />'
                : '<span class="team-logo">' + Ui.escapeHtml(t.tag && t.tag.length > 0 ? t.tag.substring(0, 1) : '?') + '</span>';
            html +=
                '<div class="col-12 col-md-6 col-lg-4">' +
                '<a class="team-card d-block h-100" href="/team/details.html?id=' + t.teamId + '">' +
                '<div class="top">' + logo + '<div><div class="name">' + Ui.escapeHtml(t.name) + '</div><div class="tag">[' + Ui.escapeHtml(t.tag) + '] · ' + Ui.escapeHtml(t.gameTitle) + '</div></div></div>' +
                '<div class="meta"><span>' + Ui.escapeHtml(t.captainName) + '</span><span>' +
                '<span class="badge badge-soft">' + t.memberCount + ' thành viên</span> ' + statusBadge(t.status) +
                '</span></div></a></div>';
        });
        html += '</div>';
        root.innerHTML = html;
    }

    async function populateGameOptions(selectedGameId) {
        var select = document.getElementById('game-select');
        try {
            var games = await Api.get('/api/teams/games');
            games.forEach(function (g) {
                var opt = document.createElement('option');
                opt.value = g.gameId;
                opt.textContent = g.title;
                if (selectedGameId && String(g.gameId) === String(selectedGameId)) opt.selected = true;
                select.appendChild(opt);
            });
        } catch (e) { /* dropdown trống nếu lỗi, không chặn trang */ }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        Ui.consumeFlash(document.getElementById('flash'));

        var params = new URLSearchParams(location.search);
        var q = params.get('Q') || '';
        var gameId = params.get('GameId') || '';
        var status = params.get('Status') || 'ACTIVE';
        var sort = params.get('Sort') || 'newest';
        var hasFilter = !!q || !!gameId || status !== 'ACTIVE';

        var form = document.getElementById('filter-form');
        form.querySelector('[name="Q"]').value = q;
        form.querySelector('[name="Status"]').value = status;
        form.querySelector('[name="Sort"]').value = sort;
        document.getElementById('clear-filter').style.display = hasFilter ? '' : 'none';

        await populateGameOptions(gameId);

        var query = new URLSearchParams();
        if (q) query.set('q', q);
        if (gameId) query.set('gameId', gameId);
        query.set('status', status);
        query.set('sort', sort);

        try {
            var list = await Api.get('/api/teams?' + query.toString());
            renderList(list, hasFilter);
        } catch (e) {
            renderList([], hasFilter);
        }
    });
})();
