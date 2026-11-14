// Các đội của tôi: mọi đội tài khoản đang là thành viên (mỗi tựa game tối đa một đội).
// Đội đang làm đội trưởng có thêm lối tắt tới bảng điều khiển và quản lý roster.
(function () {
    function logo(t) {
        return t.logoUrl
            ? '<img class="team-logo" src="' + Ui.escapeHtml(t.logoUrl) + '" alt="Logo ' + Ui.escapeHtml(t.name) + '" />'
            : '<span class="team-logo">' + Ui.escapeHtml(t.tag ? t.tag.substring(0, 1) : '?') + '</span>';
    }

    function card(t) {
        var role = t.isCaptain
            ? '<span class="badge badge-active">Đội trưởng</span>'
            : '<span class="badge badge-soft">' + (t.slot === 'SUBSTITUTE' ? 'Dự bị' : 'Chính thức') + '</span>';
        var joined = t.joinedAt ? 'Tham gia ' + new Date(t.joinedAt).toLocaleDateString('vi-VN') : '';
        var actions = '<a class="btn btn-ghost btn-sm" href="/team/details.html?id=' + t.teamId + '">Hồ sơ đội</a>';
        if (t.isCaptain) {
            actions += ' <a class="btn btn-primary btn-sm" href="/team/dashboard.html?id=' + t.teamId + '">Bảng điều khiển</a>' +
                ' <a class="btn btn-ghost btn-sm" href="/team/roster.html?id=' + t.teamId + '">Roster</a>';
        }
        return '<div class="col-12 col-md-6 col-lg-4"><div class="team-card h-100">' +
            '<div class="top">' + logo(t) + '<div class="minw-0"><div class="name">' + Ui.escapeHtml(t.name) + '</div>' +
            '<div class="tag">[' + Ui.escapeHtml(t.tag) + '] · ' + Ui.escapeHtml(t.gameTitle) + '</div></div></div>' +
            '<div class="meta"><span>' + Ui.escapeHtml(joined) + '</span><span>' +
            '<span class="badge badge-soft">' + t.memberCount + ' thành viên</span> ' + role + '</span></div>' +
            '<div class="d-flex flex-wrap gap-2" style="padding:0 20px 18px">' + actions + '</div>' +
            '</div></div>';
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        var root = document.getElementById('result-root');
        try {
            var teams = await Api.get('/api/teams/my-teams');
            if (!Array.isArray(teams) || teams.length === 0) {
                root.innerHTML = '<div class="card-etoms"><div class="empty">' +
                    '<h3>Bạn chưa tham gia đội nào</h3>' +
                    '<p>Tạo đội mới, nhập mã mời của đội trưởng, hoặc đăng tin tìm đội.</p>' +
                    '<div class="d-flex flex-wrap gap-2 justify-content-center">' +
                    '<a class="btn btn-primary" href="/team/create.html">Tạo đội mới</a>' +
                    '<a class="btn btn-ghost" href="/team/join.html">Gia nhập bằng mã</a>' +
                    '<a class="btn btn-ghost" href="/free-agent/index.html">Tìm đội</a></div>' +
                    '</div></div>';
                return;
            }
            root.innerHTML = '<div class="text-muted-etoms mb-3"><strong class="text-ink">' + teams.length + '</strong> đội</div>' +
                '<div class="row g-3">' + teams.map(card).join('') + '</div>';
        } catch (err) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tải được danh sách đội</h3>' +
                '<p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    });
})();
