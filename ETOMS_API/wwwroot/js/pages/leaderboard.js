// RNK: bảng xếp hạng đội theo TỪNG tựa game (điểm của vw_Leaderboard chỉ so được trong cùng một game).
// Mùa giải = năm khai mạc của các giải ĐÃ KẾT THÚC. Game chưa có giải kết thúc thì chưa có mùa nào: ô "Mùa giải"
// bị khóa và ghi rõ lý do (trước đây chỉ còn "Tất cả mùa" nên trông như bộ lọc bị lỗi).
(function () {
    var gameSelect = document.getElementById('ranking-game');
    var seasonSelect = document.getElementById('ranking-season');
    var body = document.getElementById('ranking-list');
    var flash = document.getElementById('ranking-flash');
    var COLS = 5;
    var seasonsByGame = {};   // gameId -> [năm...] (mới nhất trước)

    function row(text) { return '<tr><td colspan="' + COLS + '">' + text + '</td></tr>'; }

    function render(entries) {
        var seasons = seasonsByGame[gameSelect.value] || [];
        var empty = !seasons.length
            ? 'Tựa game này chưa có giải nào kết thúc nên chưa có bảng xếp hạng. Bảng xếp hạng chỉ tính các giải đã kết thúc.'
            : 'Không có đội nào được xếp hạng trong mùa đã chọn.';
        body.innerHTML = entries.length ? entries.map(function (entry) {
            var logo = entry.logoUrl
                ? '<img src="' + Ui.escapeHtml(entry.logoUrl) + '" alt="" width="32" height="32" class="rounded-circle me-2" />'
                : '';
            return '<tr><td>#' + entry.rank + '</td><td>' + logo +
                '<a href="/rankings/team.html?teamId=' + entry.teamId + '">' +
                Ui.escapeHtml(entry.teamName) + ' [' + Ui.escapeHtml(entry.teamTag) + ']</a></td>' +
                '<td>' + entry.tournamentPlayed + '</td><td>' + entry.titleCount + '</td>' +
                '<td>' + entry.achievementPoint + '</td></tr>';
        }).join('') : row(empty);
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

    async function seasonsOf(gameId) {
        if (!seasonsByGame[gameId]) {
            var response = await Api.get('/api/rankings/seasons?gameId=' + encodeURIComponent(gameId));
            seasonsByGame[gameId] = response.data || [];
        }
        return seasonsByGame[gameId];
    }

    async function loadSeasons() {
        var seasons = await seasonsOf(gameSelect.value);
        var current = seasonSelect.value;
        if (!seasons.length) {
            seasonSelect.innerHTML = '<option value="">Chưa có mùa nào (chưa có giải kết thúc)</option>';
            seasonSelect.disabled = true;
            return;
        }
        seasonSelect.disabled = false;
        seasonSelect.innerHTML = '<option value="">Tất cả mùa (' + seasons.length + ' mùa)</option>' +
            seasons.map(function (season) {
                return '<option value="' + Number(season) + '">Mùa ' + Number(season) + '</option>';
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
            // Lấy mùa của mọi game cùng lúc để ghi chú ngay trong danh sách game nào chưa có giải kết thúc.
            await Promise.all(games.map(function (g) { return seasonsOf(g.gameId).catch(function () { seasonsByGame[g.gameId] = []; }); }));
            gameSelect.innerHTML = games.map(function (game) {
                var none = !(seasonsByGame[game.gameId] || []).length;
                return '<option value="' + game.gameId + '">' + Ui.escapeHtml(game.title) + (none ? ' — chưa có giải kết thúc' : '') + '</option>';
            }).join('');
            // ?gameId= trên đường dẫn (vd từ trang đội) chọn sẵn game đó; không có thì mở game đầu tiên đã có mùa giải.
            var wanted = Ui.qsInt('gameId');
            if (wanted !== null && games.some(function (g) { return g.gameId === wanted; })) gameSelect.value = String(wanted);
            else {
                var first = games.find(function (g) { return (seasonsByGame[g.gameId] || []).length; });
                if (first) gameSelect.value = String(first.gameId);
            }
            await onGameChange();
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải bộ lọc bảng xếp hạng.');
        }
    });

    gameSelect.addEventListener('change', onGameChange);
    seasonSelect.addEventListener('change', load);
})();
