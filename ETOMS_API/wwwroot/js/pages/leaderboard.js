(function () {
    var gameSelect = document.getElementById('ranking-game');
    var seasonSelect = document.getElementById('ranking-season');
    var body = document.getElementById('ranking-list');
    var flash = document.getElementById('ranking-flash');

    function render(entries) {
        body.innerHTML = entries.length ? entries.map(function (entry) {
            var logo = entry.logoUrl
                ? '<img src="' + Ui.escapeHtml(entry.logoUrl) + '" alt="" width="32" height="32" class="rounded-circle me-2" />'
                : '';
            return '<tr><td>#' + entry.rank + '</td><td>' + logo +
                '<a href="/rankings/team.html?teamId=' + entry.teamId + '">' +
                Ui.escapeHtml(entry.teamName) + ' [' + Ui.escapeHtml(entry.teamTag) + ']</a></td>' +
                '<td>' + Ui.escapeHtml(entry.gameName) + '</td><td>' + entry.season + '</td>' +
                '<td>' + entry.tournamentPlayed + '</td><td>' + entry.titleCount + '</td>' +
                '<td><strong>' + entry.achievementPoint + '</strong></td></tr>';
        }).join('') : '<tr><td colspan="7">Chưa có thành tích đã chốt phù hợp bộ lọc.</td></tr>';
    }

    async function load() {
        var query = new URLSearchParams();
        if (gameSelect.value) query.set('gameId', gameSelect.value);
        if (seasonSelect.value) query.set('season', seasonSelect.value);
        body.innerHTML = '<tr><td colspan="7">Đang tải…</td></tr>';
        try {
            var response = await Api.get('/api/rankings?' + query.toString());
            var entries = response.data || [];
            render(entries);
        } catch (error) {
            body.innerHTML = '<tr><td colspan="7">Không thể tải bảng xếp hạng.</td></tr>';
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải bảng xếp hạng.');
        }
    }

    async function loadSeasons() {
        var query = gameSelect.value ? '?gameId=' + encodeURIComponent(gameSelect.value) : '';
        var response = await Api.get('/api/rankings/seasons' + query);
        var current = seasonSelect.value;
        seasonSelect.innerHTML = '<option value="">Tất cả mùa</option>' +
            (response.data || []).map(function (season) {
                return '<option value="' + season + '">' + season + '</option>';
            }).join('');
        if (Array.from(seasonSelect.options).some(function (option) { return option.value === current; })) {
            seasonSelect.value = current;
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        try {
            var response = await Api.get('/api/home/summary');
            var games = response.data.games || [];
            gameSelect.innerHTML += games.map(function (game) {
                return '<option value="' + game.gameId + '">' + Ui.escapeHtml(game.title) + '</option>';
            }).join('');
            await loadSeasons();
            await load();
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải bộ lọc bảng xếp hạng.');
        }
    });

    document.getElementById('ranking-apply').addEventListener('click', load);
    gameSelect.addEventListener('change', async function () {
        try {
            await loadSeasons();
            await load();
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể cập nhật mùa giải.');
        }
    });
})();
