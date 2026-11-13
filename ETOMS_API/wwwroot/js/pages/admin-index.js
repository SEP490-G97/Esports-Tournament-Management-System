// Trang tổng hợp cho Admin: đếm việc chờ xử lý từ các API có sẵn và dẫn tới công cụ tương ứng.
// Không có nghiệp vụ mới — mọi thao tác vẫn làm ở trang đích.
// Các ô hiện ngay, số liệu ô nào về trước thì điền trước (không chờ API chậm nhất).
(function () {
    function count(value) {
        if (Array.isArray(value)) return value.length;
        if (value && Array.isArray(value.items)) return value.items.length;
        return null;
    }

    var TILES = [
        {
            key: 'review', overline: 'Chờ xét duyệt', title: 'Hồ sơ đơn vị tổ chức', primaryWhenPositive: true,
            text: 'Duyệt hoặc từ chối hồ sơ đăng ký đơn vị (ORG-02). Từ chối phải ghi lý do.',
            href: '/organization/index.html#review-section', action: 'Mở hàng chờ duyệt',
            url: '/api/organizations/review-queue', pick: count
        },
        {
            key: 'disputes', overline: 'Tranh chấp', title: 'Hàng đợi tranh chấp', primaryWhenPositive: true,
            text: 'Các trận có báo cáo lệch nhau hoặc bị khiếu nại, chờ phán quyết (RES-06/07).',
            href: '/result/disputes.html', action: 'Xem hàng đợi',
            url: '/api/disputes', pick: count
        },
        {
            key: 'accounts', overline: 'Tài khoản', title: 'Quản lý tài khoản',
            text: 'Tìm kiếm, lọc theo vai trò / trạng thái, khóa và mở khóa tài khoản (ADM-01/02).',
            href: '/admin/user.html', action: 'Quản lý tài khoản',
            url: '/api/admin/accounts?page=1&pageSize=1', pick: function (d) { return d && typeof d.total === 'number' ? d.total : null; }
        },
        {
            key: 'rules', overline: 'Luật thi đấu', title: 'Bộ luật theo tựa game',
            text: 'Quản lý bộ luật, bản đồ và vị trí cho từng game (GRL-01..03).',
            href: '/admin/game-rules.html', action: 'Quản lý bộ luật',
            url: '/api/game-rules', pick: function (d) {
                var games = d && d.games ? d.games : [];
                return games.reduce(function (sum, g) { return sum + (g.rulesets ? g.rulesets.length : 0); }, 0);
            }
        },
        {
            key: 'orgs', overline: 'Đơn vị', title: 'Đơn vị tổ chức',
            text: 'Danh sách công khai các đơn vị tổ chức giải và điểm uy tín.',
            href: '/organization/index.html', action: 'Xem danh sách'
        },
        {
            key: 'teams', overline: 'Cộng đồng', title: 'Đội tuyển',
            text: 'Toàn bộ đội đang hoạt động trên hệ thống.',
            href: '/team/index.html', action: 'Xem đội tuyển',
            url: '/api/teams', pick: count
        }
    ];

    function tile(t) {
        return '<div class="card-etoms admin-tile" data-tile="' + t.key + '">' +
            '<span class="profile-overline">' + Ui.escapeHtml(t.overline) + '</span>' +
            (t.url ? '<div class="num">…</div>' : '') +
            '<h2>' + Ui.escapeHtml(t.title) + '</h2>' +
            '<p>' + Ui.escapeHtml(t.text) + '</p>' +
            '<a class="btn btn-ghost btn-sm" href="' + t.href + '">' + Ui.escapeHtml(t.action) + '</a>' +
            '</div>';
    }

    async function fill(t) {
        var box = document.querySelector('[data-tile="' + t.key + '"]');
        var value = null;
        try {
            var res = await Api.get(t.url);
            value = t.pick(res.data !== undefined ? res.data : res);
        } catch (e) { value = null; }
        box.querySelector('.num').textContent = value === null || value === undefined ? '—' : String(value);
        if (t.primaryWhenPositive && value > 0) {
            var btn = box.querySelector('.btn');
            btn.classList.remove('btn-ghost');
            btn.classList.add('btn-primary');
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ADMIN']);
        if (!me) return;

        document.getElementById('admin-root').innerHTML = '<div class="admin-grid">' + TILES.map(tile).join('') + '</div>';
        TILES.filter(function (t) { return t.url; }).forEach(fill);
    });
})();
