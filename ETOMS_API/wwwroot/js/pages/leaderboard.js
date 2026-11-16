// RNK: bảng xếp hạng đội theo TỪNG tựa game (điểm của vw_Leaderboard chỉ so được trong cùng một game).
(function () {
    var gameSelect = document.getElementById('ranking-game');
    var seasonSelect = document.getElementById('ranking-season');
    var body = document.getElementById('ranking-list');
    var flash = document.getElementById('ranking-flash');
    var COLS = 5;

    function row(text) { return '<tr><td colspan="' + COLS + '">' + text + '</td></tr>'; }

    function render(entries) {
        body.innerHTML = entries.length ? entries.map(function (entry) {
            var logo = entry.logoUrl
                ? '<img src="' + Ui.escapeHtml(entry.logoUrl) + '" alt="" width="32" height="32" class="rounded-circle me-2" />'
                : '';
            return '<tr><td>#' + entry.rank + '</td><td>' + logo +
                '<a href="/rankings/team.html?teamId=' + entry.teamId + '">' +
                Ui.escapeHtml(entry.teamName) + ' [' + Ui.escapeHtml(entry.teamTag) + ']</a></td>' +
                '<td>' + entry.tournamentPlayed + '</td><td>' + entry.titleCount + '</td>' +
                '<td>' + entry.achievementPoint + '</td></tr>';
        }).join('') : row('Tựa game này chưa có giải nào kết thúc trong mùa đã chọn.');
    }

    async function load() {
        if (!gameSelect.value) return;
        var query = new URLSearchParams({ gameId: gameSelect.value });
        if (seasonSelect.value) query.set('season', seasonSelect.value);
        body.innerHTML = row('Đang tải…');
        try {
            var response = await Api.get('/api/rankings?' + query.toString());
            render(response.data || []);
        } catch (error) {
            body.innerHTML = row('Không thể tải bảng xếp hạng.');
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải bảng xếp hạng.');
        }
    }

    async function loadSeasons() {
        var response = await Api.get('/api/rankings/seasons?gameId=' + encodeURIComponent(gameSelect.value));
        var current = seasonSelect.value;
        seasonSelect.innerHTML = '<option value="">Tất cả mùa</option>' +
            (response.data || []).map(function (season) {
                return '<option value="' + season + '">' + season + '</option>';
            }).join('');
        if (Array.from(seasonSelect.options).some(function (option) { return option.value === current; })) {
            seasonSelect.value = current;
        }
    }

    async function onGameChange() {
        Ui.clearAlert(flash);
        try {
            await loadSeasons();
            await load();
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể cập nhật mùa giải.');
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        try {
            var response = await Api.get('/api/home/summary');
            var games = response.games || [];
            if (!games.length) { gameSelect.innerHTML = '<option value="">Chưa có tựa game</option>'; body.innerHTML = row('Chưa có tựa game nào.'); return; }
            gameSelect.innerHTML = games.map(function (game) {
                return '<option value="' + game.gameId + '">' + Ui.escapeHtml(game.title) + '</option>';
            }).join('');
            // ?gameId= trên đường dẫn (vd từ trang đội) chọn sẵn game đó; không có thì lấy game đầu danh sách.
            var wanted = Ui.qsInt('gameId');
            if (wanted !== null && games.some(function (g) { return g.gameId === wanted; })) gameSelect.value = String(wanted);
            else {
                // Không chỉ định: mở sẵn game đầu tiên đã có mùa giải (có giải kết thúc), tránh bảng trống.
                for (var i = 0; i < games.length; i++) {
                    var s = await Api.get('/api/rankings/seasons?gameId=' + games[i].gameId);
                    if ((s.data || []).length) { gameSelect.value = String(games[i].gameId); break; }
                }
            }
            await onGameChange();
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải bộ lọc bảng xếp hạng.');
        }
    });

    gameSelect.addEventListener('change', onGameChange);
    seasonSelect.addEventListener('change', load);
})();
