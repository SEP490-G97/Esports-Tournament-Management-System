(function () {
    // Tải danh sách bảng tin tuyển quân (FA-03)
    async function loadBoard() {
        var container = document.getElementById('boardContainer');
        var type = document.getElementById('filterType').value;
        container.innerHTML = '<div class="empty"><h3>Đang tải...</h3></div>';

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
                html += '<div style="background: var(--bg-2); border: 1px solid var(--border); border-radius: 12px; padding: 18px; display: flex; justify-content: space-between; align-items: center; gap: 16px; flex-wrap: wrap;">';
                html += '<div style="flex: 1; min-width: 260px;">';
                html += '<span class="badge ' + badgeClass + '">' + Ui.escapeHtml(item.postType) + '</span>';
                html += '<h4 style="color: var(--ink); margin: 8px 0 4px; font-size: 1.05rem; font-weight: 700;">' + Ui.escapeHtml(item.title) + '</h4>';
                html += '<p style="color: var(--muted); font-size: 0.9rem; margin: 0 0 8px;">' + Ui.escapeHtml(item.content) + '</p>';
                html += '<small style="color: var(--muted-2); font-size: 0.8rem;">Tác giả: ' + Ui.escapeHtml(item.authorName) + (item.teamName ? ' · Đội tuyển: ' + Ui.escapeHtml(item.teamName) : '') + '</small>';
                html += '</div>';

                if (item.postType === 'LFP') {
                    html += '<button onclick="applyPost(' + item.postId + ')" class="btn btn-primary" style="padding: 9px 16px; font-size: 0.85rem;">Ứng tuyển</button>';
                }
                html += '</div>';
            });
            html += '</div>';
            container.innerHTML = html;
        } catch (err) {
            container.innerHTML = '<div class="empty"><h3 style="color: var(--danger);">Lỗi tải dữ liệu</h3><p>' + Ui.escapeHtml(err.message) + '</p></div>';
        }
    }

    // Hành động ứng tuyển (FA-04)
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

    document.addEventListener('DOMContentLoaded', async function () {
        // Kiểm tra xác thực hệ thống chung
        var user = await Ui.requireAuth();
        if (!user) return;

        Ui.consumeFlash(document.getElementById('flash'));
        loadBoard();

        // Xử lý sự kiện Submit Form LFP (FA-01)
        var lfpForm = document.getElementById('lfpForm');
        lfpForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearFieldErrors(lfpForm);

            var payload = {
                teamId: parseInt(document.getElementById('lfpTeamId').value, 10),
                title: document.getElementById('lfpTitle').value,
                content: document.getElementById('lfpContent').value
            };

            try {
                await Api.post('/api/Recruitment/lfp', payload);
                Ui.setFlash('Đăng tin tuyển thành viên (LFP) thành công!', 'success');
                location.reload();
            } catch (err) {
                Ui.applyFieldErrors(lfpForm, err.fieldErrors);
                alert(err.message || 'Có lỗi xảy ra khi tạo tin LFP.');
            }
        });

        // Xử lý sự kiện Submit Form LFT (FA-02)
        var lftForm = document.getElementById('lftForm');
        lftForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearFieldErrors(lftForm);

            var payload = {
                gameId: parseInt(document.getElementById('lftGameId').value, 10),
                title: document.getElementById('lftTitle').value,
                content: document.getElementById('lftContent').value
            };

            try {
                await Api.post('/api/Recruitment/lft', payload);
                Ui.setFlash('Đăng tin tìm đội (LFT) thành công!', 'success');
                location.reload();
            } catch (err) {
                Ui.applyFieldErrors(lftForm, err.fieldErrors);
                alert(err.message || 'Có lỗi xảy ra khi tạo tin LFT.');
            }
        });
    });
})();