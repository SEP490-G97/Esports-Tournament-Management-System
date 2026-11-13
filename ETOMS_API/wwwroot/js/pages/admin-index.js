// Trang tổng hợp cho Admin: đếm việc chờ xử lý từ các API có sẵn và dẫn tới công cụ tương ứng.
// Không có nghiệp vụ mới — mọi thao tác vẫn làm ở trang đích.
(function () {
    function count(value) {
        if (Array.isArray(value)) return value.length;
        if (value && Array.isArray(value.items)) return value.items.length;
        return null;
    }

    async function safeCount(url, pick) {
        try {
            var res = await Api.get(url);
            return pick(res.data !== undefined ? res.data : res);
        } catch (e) {
            return null;
        }
    }

    function tile(t) {
        var num = t.count === null || t.count === undefined ? '—' : String(t.count);
        return '<div class="card-etoms admin-tile">' +
            '<span class="profile-overline">' + Ui.escapeHtml(t.overline) + '</span>' +
            '<div class="num">' + Ui.escapeHtml(num) + '</div>' +
            '<h2>' + Ui.escapeHtml(t.title) + '</h2>' +
            '<p>' + Ui.escapeHtml(t.text) + '</p>' +
            (t.href ? '<a class="btn ' + (t.primary ? 'btn-primary' : 'btn-ghost') + ' btn-sm" href="' + t.href + '">' + Ui.escapeHtml(t.action) + '</a>' : '') +
            '</div>';
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ADMIN']);
        if (!me) return;

        var results = await Promise.all([
            safeCount('/api/organizations/review-queue', count),
            safeCount('/api/game-rules', function (d) {
                var games = d && d.games ? d.games : [];
                return games.reduce(function (sum, g) { return sum + (g.rulesets ? g.rulesets.length : 0); }, 0);
            }),
            safeCount('/api/disputes', count),
            safeCount('/api/organizations', count),
            safeCount('/api/teams', count)
        ]);

        var tiles = [
            { overline: 'Chờ xét duyệt', count: results[0], title: 'Hồ sơ đơn vị tổ chức', text: 'Duyệt hoặc từ chối hồ sơ đăng ký đơn vị (ORG-02). Từ chối phải ghi lý do.', href: '/organization/index.html#review-section', action: 'Mở hàng chờ duyệt', primary: results[0] > 0 },
            { overline: 'Tranh chấp', count: results[2], title: 'Hàng đợi tranh chấp', text: 'Các trận có báo cáo lệch nhau hoặc bị khiếu nại, chờ phán quyết (RES-06/07).', href: '/result/disputes.html', action: 'Xem hàng đợi', primary: results[2] > 0 },
            { overline: 'Luật thi đấu', count: results[1], title: 'Bộ luật theo tựa game', text: 'Quản lý bộ luật, bản đồ và vị trí cho từng game (GRL-01..03).', href: '/admin/game-rules.html', action: 'Quản lý bộ luật' },
            { overline: 'Đơn vị', count: results[3], title: 'Đơn vị tổ chức đang hoạt động', text: 'Danh sách công khai các đơn vị tổ chức giải.', href: '/organization/index.html', action: 'Xem danh sách' },
            { overline: 'Cộng đồng', count: results[4], title: 'Đội tuyển', text: 'Toàn bộ đội đang hoạt động trên hệ thống.', href: '/team/index.html', action: 'Xem đội tuyển' },
            { overline: 'Tài khoản', count: null, title: 'Khóa / mở khóa tài khoản', text: 'Đã có API (AUTH-07: /api/admin/accounts/{id}/ban, /unban) nhưng chưa có giao diện quản lý tài khoản.', href: null }
        ];

        document.getElementById('admin-root').innerHTML = '<div class="admin-grid">' + tiles.map(tile).join('') + '</div>';
    });
})();
