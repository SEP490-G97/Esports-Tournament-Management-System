(function () {
    function fmtMonthYear(iso) {
        var d = new Date(iso);
        return String(d.getUTCMonth() + 1).padStart(2, '0') + '/' + d.getUTCFullYear();
    }

    function render(team, isCaptain, isMember) {
        var root = document.getElementById('team-root');
        var logo = team.logoUrl
            ? '<img class="logo-big" src="' + Ui.escapeHtml(team.logoUrl) + '" alt="Logo ' + Ui.escapeHtml(team.name) + '" />'
            : '<span class="logo-big">' + Ui.escapeHtml(team.tag && team.tag.length > 0 ? team.tag.substring(0, 1) : '?') + '</span>';
        var statusBadge = team.status === 'ACTIVE'
            ? '<span class="badge badge-active">Đang hoạt động</span>'
            : '<span class="badge" style="background:var(--danger-bg);color:var(--danger-ink)">Đã giải tán</span>';

        var membersHtml;
        if (!team.members.length) {
            membersHtml = '<div class="card-etoms"><div class="empty"><h3>Chưa có thành viên</h3></div></div>';
        } else {
            membersHtml = '<div class="row g-3">';
            team.members.forEach(function (m) {
                membersHtml +=
                    '<div class="col-12 col-md-6"><div class="member-card' + (m.isCaptain ? ' cap' : '') + '">' +
                    '<span class="av">' + Ui.escapeHtml(m.fullName && m.fullName.length > 0 ? m.fullName.substring(0, 1) : '?') + '</span>' +
                    '<div class="grow"><div class="nm">' + Ui.escapeHtml(m.fullName) + '</div><div class="rl">' + Ui.escapeHtml(m.roleLabel) + '</div></div>' +
                    (m.isCaptain ? '<span class="badge badge-gold">Đội trưởng</span>' : '') +
                    '</div></div>';
            });
            membersHtml += '</div>';
        }

        var actionHtml = '<a class="btn btn-ghost" href="/team/index.html">← Về danh sách đội</a>';
        if (isCaptain) {
            actionHtml += '<a class="btn btn-primary" href="/team/dashboard.html?id=' + team.teamId + '">Quản trị đội</a>';
        } else if (isMember) {
            actionHtml += '<button type="button" class="btn btn-ghost" style="color:var(--danger-ink)" id="leave-btn">Rời đội</button>';
        }

        root.innerHTML =
            '<div class="eyebrow">Hồ sơ đội · Mã đội #' + team.teamId + '</div>' +
            '<div class="card-etoms"><div class="body"><div class="team-hero">' + logo +
            '<div style="flex:1;min-width:260px"><h1>' + Ui.escapeHtml(team.name) + ' <span style="color:var(--muted);font-weight:700">[' + Ui.escapeHtml(team.tag) + ']</span></h1>' +
            '<div class="sub"><span>' + Ui.escapeHtml(team.gameTitle) + '</span>' +
            '<span>· Đội trưởng: <strong style="color:var(--ink)">' + Ui.escapeHtml(team.captainName) + '</strong></span>' +
            '<span>· Thành lập ' + fmtMonthYear(team.createdAt) + '</span>' + statusBadge +
            '</div></div></div></div></div>' +
            '<div class="row g-3 my-4">' +
            '<div class="col-6 col-md-3"><div class="stat-card h-100"><div class="label">Sĩ số</div><div class="num accent">' + team.memberCount + '</div><div class="sub">thành viên</div></div></div>' +
            '<div class="col-6 col-md-3"><div class="stat-card h-100"><div class="label">Chính thức</div><div class="num">' + team.starterCount + '</div><div class="sub">người</div></div></div>' +
            '<div class="col-6 col-md-3"><div class="stat-card h-100"><div class="label">Dự bị</div><div class="num">' + team.subCount + '</div><div class="sub">người</div></div></div>' +
            '<div class="col-6 col-md-3"><div class="stat-card h-100"><div class="label">Trạng thái</div><div class="num" style="font-size:1.3rem;margin-top:14px">' + (team.status === 'ACTIVE' ? 'Hoạt động' : 'Giải tán') + '</div></div></div>' +
            '</div>' +
            '<div class="section-title">Danh sách thành viên</div>' +
            '<div class="section-sub">Roster hiện tại của đội (' + team.memberCount + ' người).</div>' +
            membersHtml +
            '<div class="btn-row" style="margin-top:26px">' + actionHtml + '</div>';

        var leaveBtn = document.getElementById('leave-btn');
        if (leaveBtn) {
            leaveBtn.addEventListener('click', async function () {
                var ok = await window.etomsConfirm('Bạn chắc chắn muốn rời đội ' + team.name + '?');
                if (!ok) return;
                try {
                    var res = await Api.post('/api/teams/' + team.teamId + '/leave');
                    Ui.setFlash(res.message, 'success');
                    location.href = '/team/details.html?id=' + team.teamId;
                } catch (err) {
                    Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
                }
            });
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        Ui.consumeFlash(document.getElementById('flash'));
        var id = Ui.qsInt('id');
        var root = document.getElementById('team-root');
        if (!id) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã đội</h3></div></div>'; return; }

        var me = { isAuthenticated: false };
        try { me = await Api.get('/api/dev-auth/me'); } catch (e) {}

        try {
            var team = await Api.get('/api/teams/' + id);
            var isCaptain = me.isAuthenticated && me.userId === team.captainAccountId;
            var isMember = me.isAuthenticated && team.members.some(function (m) { return m.accountId === me.userId; });
            render(team, isCaptain, isMember);
        } catch (err) {
            if (err.status === 404) {
                root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy đội</h3></div></div>';
            } else {
                root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
            }
        }
    });
})();
