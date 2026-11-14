(function () {
    // 1. Tải danh sách Đội tuyển & Game từ DB để đổ vào Dropdown
    async function loadSelectOptions() {
        try {
            // Nạp Đội tuyển của tôi
            var teamSelect = document.getElementById('lfpTeamId');
            if (teamSelect) {
                try {
                    var teamsRes = await Api.get('/api/teams/mine');
                    var teamData = (teamsRes && teamsRes.data !== undefined) ? teamsRes.data : teamsRes;
                    var teams = [];
                    if (Array.isArray(teamData)) {
                        teams = teamData;
                    } else if (teamData && teamData.teamId) {
                        teams = [teamData];
                    }

                    if (teams.length > 0) {
                        teamSelect.innerHTML = teams.map(t => `<option value="${t.teamId}">${Ui.escapeHtml(t.name)} [${Ui.escapeHtml(t.tag)}]</option>`).join('');
                    } else {
                        teamSelect.innerHTML = '<option value="">-- Bạn chưa tham gia đội tuyển nào --</option>';
                    }
                } catch (err) {
                    teamSelect.innerHTML = '<option value="">-- Chưa thể tải đội tuyển --</option>';
                }
            }

            // Nạp Tựa game
            var gameSelect = document.getElementById('lftGameId');
            if (gameSelect) {
                try {
                    var gamesRes = await Api.get('/api/teams/games');
                    var gameData = (gamesRes && gamesRes.data !== undefined) ? gamesRes.data : gamesRes;
                    var games = Array.isArray(gameData) ? gameData : [];
                    if (games.length > 0) {
                        gameSelect.innerHTML = games.map(g => `<option value="${g.gameId}">${Ui.escapeHtml(g.title)}</option>`).join('');
                    } else {
                        gameSelect.innerHTML = '<option value="">-- Chưa có tựa game khả dụng --</option>';
                    }
                } catch (err) {
                    gameSelect.innerHTML = '<option value="">-- Không thể tải danh sách game --</option>';
                }
            }
        } catch (e) {
            console.warn('Lỗi nạp danh sách dropdown:', e);
        }
    }

    // 2. Tải bảng tin hiển thị Tên Đội & Tên Game thật
    async function loadBoard() {
        var container = document.getElementById('boardContainer');
        var type = document.getElementById('filterType').value;
        container.innerHTML = '<div class="empty"><h3>Đang tải dữ liệu từ cơ sở dữ liệu...</h3></div>';

        try {
            var res = await Api.get('/api/Recruitment/board?PostType=' + type + '&PageIndex=1&PageSize=10');
            var items = res.data || [];

            if (items.length === 0) {
                container.innerHTML = '<div class="empty"><h3>Không có tin đăng nào</h3><p>Hiện chưa có bản ghi phù hợp với bộ lọc.</p></div>';
                return;
            }

            var html = '<div style="display: grid; gap: 14px;">';
            items.forEach(function (item) {
                var badgeClass = item.postType === 'LFP' ? 'badge-active' : 'badge-gold';
                var targetInfo = item.teamName
                    ? `🛡️ Đội tuyển: <b>${Ui.escapeHtml(item.teamName)}</b>`
                    : `🎮 Tựa game: <b>${Ui.escapeHtml(item.gameTitle || 'Chung')}</b>`;

                html += '<div style="background: var(--bg-2); border: 1px solid var(--border); border-radius: 12px; padding: 18px; display: flex; justify-content: space-between; align-items: center; gap: 16px; flex-wrap: wrap;">';
                html += '<div style="flex: 1; min-width: 260px;">';
                html += '<div style="display: flex; gap: 8px; align-items: center; margin-bottom: 6px;">';
                html += '<span class="badge ' + badgeClass + '">' + Ui.escapeHtml(item.postType) + '</span>';
                html += '<span style="font-size: 0.82rem; color: var(--cyan);">' + targetInfo + '</span>';
                html += '</div>';
                html += '<h4 style="color: var(--ink); margin: 4px 0; font-size: 1.1rem; font-weight: 700;">' + Ui.escapeHtml(item.title) + '</h4>';
                html += '<p style="color: var(--muted); font-size: 0.9rem; margin: 6px 0; white-space: pre-line;">' + Ui.escapeHtml(item.content) + '</p>';
                html += '<small style="color: var(--muted-2); font-size: 0.8rem;">✍️ Tác giả: ' + Ui.escapeHtml(item.authorName) + '</small>';
                html += '</div>';

                if (item.postType === 'LFP') {
                    html += '<button onclick="applyPost(' + item.postId + ')" class="btn btn-primary" style="padding: 9px 16px; font-size: 0.85rem;">Ứng tuyển ngay</button>';
                }
                html += '</div>';
            });
            html += '</div>';
            container.innerHTML = html;
        } catch (err) {
            container.innerHTML = '<div class="empty"><h3 style="color: var(--danger);">Lỗi tải dữ liệu</h3><p>' + Ui.escapeHtml(err.message) + '</p></div>';
        }
    }

    // 3. Ứng tuyển
    window.applyPost = async function (postId) {
        var msg = prompt('Nhập lời nhắn gửi đến Đội trưởng:');
        if (msg === null) return;

        try {
            await Api.post('/api/Recruitment/apply', { postId: postId, message: msg });
            alert('Nộp đơn ứng tuyển thành công! Hệ thống đang chờ Đội trưởng phê duyệt.');
        } catch (err) {
            alert('Lỗi: ' + err.message);
        }
    };

    // Khởi chạy khi DOM đã sẵn sàng
    document.addEventListener('DOMContentLoaded', async function () {
        var user = await Ui.requireAuth();
        if (!user) return;

        Ui.consumeFlash(document.getElementById('flash'));
        await loadSelectOptions();
        await loadBoard();

        // Submit Form LFP
        var lfpForm = document.getElementById('lfpForm');
        lfpForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearFieldErrors(lfpForm);

            var payload = {
                teamId: parseInt(document.getElementById('lfpTeamId').value, 10),
                requiredRole: document.getElementById('lfpRequiredRole').value,
                requiredRank: document.getElementById('lfpRequiredRank').value,
                description: document.getElementById('lfpDescription').value
            };

            try {
                await Api.post('/api/Recruitment/lfp', payload);
                Ui.setFlash('Đăng tin tuyển thành viên (LFP) thành công!', 'success');
                location.reload();
            } catch (err) {
                Ui.applyFieldErrors(lfpForm, err.fieldErrors);
                alert(err.message || 'Có lỗi xảy ra.');
            }
        });

        // Submit Form LFT
        var lftForm = document.getElementById('lftForm');
        lftForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearFieldErrors(lftForm);

            var payload = {
                gameId: parseInt(document.getElementById('lftGameId').value, 10),
                preferredRole: document.getElementById('lftPreferredRole').value,
                currentRank: document.getElementById('lftCurrentRank').value,
                description: document.getElementById('lftDescription').value
            };

            try {
                await Api.post('/api/Recruitment/lft', payload);
                Ui.setFlash('Đăng tin tìm đội (LFT) thành công!', 'success');
                location.reload();
            } catch (err) {
                Ui.applyFieldErrors(lftForm, err.fieldErrors);
                alert(err.message || 'Có lỗi xảy ra.');
            }
        });
    });
})();