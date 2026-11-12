(function () {
    var teamId = Number(new URLSearchParams(location.search).get('teamId'));
    var flash = document.getElementById('team-ranking-flash');
    var body = document.getElementById('team-honors');
    var placementLabels = {
        CHAMPION: 'Vô địch',
        RUNNER_UP: 'Á quân',
        THIRD: 'Hạng ba',
        PARTICIPANT: 'Tham gia'
    };

    document.addEventListener('DOMContentLoaded', async function () {
        if (!Number.isInteger(teamId) || teamId <= 0) {
            Ui.showAlert(flash, 'danger', 'Mã đội tuyển không hợp lệ.');
            return;
        }
        try {
            var response = await Api.get('/api/rankings/teams/' + teamId);
            var team = response.data;
            document.title = team.teamName + ' · ETOMS';
            document.getElementById('ranked-team-game').textContent = team.gameName + ' · ' + team.teamTag;
            document.getElementById('ranked-team-name').textContent = team.teamName;
            document.getElementById('ranked-team-created').textContent =
                'Thành lập ' + new Date(team.createdAt).toLocaleDateString('vi-VN');
            document.getElementById('ranked-team').hidden = false;
            body.innerHTML = team.honors.length ? team.honors.map(function (honor) {
                return '<tr><td>' + Ui.escapeHtml(honor.tournamentName) + '</td><td>' + honor.season +
                    '</td><td>' + honor.rank + '</td><td>' +
                    Ui.escapeHtml(placementLabels[honor.placement] || honor.placement) + '</td></tr>';
            }).join('') : '<tr><td colspan="4">Chưa có danh hiệu từ giải đã kết thúc.</td></tr>';
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải hồ sơ đội tuyển.');
        }
    });
})();
