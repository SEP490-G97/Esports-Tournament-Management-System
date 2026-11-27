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
                'Thành lập ' + Ui.parseUtc(team.createdAt).toLocaleDateString('vi-VN');
            document.getElementById('ranked-team').hidden = false;
            // Đội lâu năm có nhiều giải: tìm theo tên giải/mùa + phân trang 10 dòng (góp ý 27/11).
            Ui.tablePager(body, {
                items: team.honors, pageSize: 10, cols: 4, emptyText: 'Chưa có danh hiệu từ giải đã kết thúc.',
                placeholder: 'Tìm theo tên giải, mùa...',
                text: function (h) { return h.tournamentName + ' ' + h.season + ' ' + (placementLabels[h.placement] || ''); },
                render: function (honor) {
                    return '<tr><td>' + Ui.escapeHtml(honor.tournamentName) + '</td><td>' + honor.season +
                        '</td><td>' + honor.rank + '</td><td>' +
                        Ui.escapeHtml(placementLabels[honor.placement] || honor.placement) + '</td></tr>';
                }
            });
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải hồ sơ đội tuyển.');
        }
    });
})();
