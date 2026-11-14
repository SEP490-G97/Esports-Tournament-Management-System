(function () {
    var root = document.getElementById('organizer-tournaments');
    var flash = document.getElementById('organizer-flash');

    function statusLabel(status) {
        return {
            DRAFT: 'Bản nháp',
            PUBLISHED: 'Sắp diễn ra',
            ONGOING: 'Đang diễn ra',
            COMPLETED: 'Đã kết thúc',
            CANCELLED: 'Đã hủy'
        }[status] || status;
    }

    function fmtDate(iso) {
        if (!iso) return '—';
        var d = new Date(iso);
        return d.toLocaleDateString('vi-VN');
    }

    function render(tournaments) {
        if (!tournaments.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Bạn chưa tổ chức giải nào</h3>' +
                '<p>Bấm "Tạo giải mới" ở trên để bắt đầu thiết lập giải đấu đầu tiên.</p></div></div>';
            return;
        }
        root.innerHTML = tournaments.map(function (t) {
            return '<article class="feature-card">' +
                '<div class="d-flex justify-content-between gap-3 flex-wrap">' +
                '<div><h3>' + Ui.escapeHtml(t.name) + '</h3>' +
                '<div class="muted-copy">' + Ui.escapeHtml(t.gameName) + ' · Khởi tranh ' + fmtDate(t.startAt) + '</div></div>' +
                '<span class="badge badge-soft">' + Ui.escapeHtml(statusLabel(t.status)) + '</span>' +
                '</div>' +
                '<div class="btn-row mt-3">' +
                '<a class="btn btn-ghost" href="/tournament/dashboard.html?id=' + t.tournamentId + '">Bảng điều hành</a>' +
                '<a class="btn btn-ghost" href="/registration/review.html?tournamentId=' + t.tournamentId + '">Duyệt đăng ký</a>' +
                '<a class="btn btn-ghost" href="/bracket/manage.html?id=' + t.tournamentId + '">Nhánh đấu</a>' +
                '<a class="btn btn-ghost" href="/tournament/edit.html?id=' + t.tournamentId + '">Sửa giải</a>' +
                '</div></article>';
        }).join('');
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        try {
            var response = await Api.get('/api/marketing/mine/tournaments');
            render(response.data || []);
        } catch (error) {
            root.innerHTML = '';
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải danh sách giải đấu.');
        }
    });
})();
