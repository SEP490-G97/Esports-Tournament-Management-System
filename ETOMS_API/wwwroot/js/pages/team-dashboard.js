(function () {
    function render(d) {
        var root = document.getElementById('dash-root');
        var logo = d.logoUrl
            ? '<img class="logo-big" style="width:84px;height:84px" src="' + Ui.escapeHtml(d.logoUrl) + '" alt="Logo ' + Ui.escapeHtml(d.name) + '" />'
            : '<span class="logo-big" style="width:84px;height:84px;font-size:1.9rem">' + Ui.escapeHtml(d.tag && d.tag.length > 0 ? d.tag.substring(0, 1) : '?') + '</span>';

        var underStrengthAlert = d.isUnderStrength
            ? '<div class="alert" style="background:rgba(246,167,35,.1);border:1px solid rgba(246,167,35,.3);color:var(--warn);margin-top:20px">' +
              '⚠ Đội đang <strong>thiếu người chính thức</strong>: hiện ' + d.starterCount + '/' + d.maxStarters + ' theo luật ' + Ui.escapeHtml(d.gameTitle) + '. Hãy tuyển thêm hoặc dùng mã mời.</div>'
            : '';

        var pendingBanner = d.pendingCount > 0
            ? '<div class="pending-banner">⏳ Có <b>' + d.pendingCount + ' đơn</b> đang chờ duyệt. ' +
              '<a href="/team/roster.html?id=' + d.teamId + '" style="color:var(--warn);text-decoration:underline;margin-left:4px">Xem &amp; duyệt ngay →</a></div>'
            : '';

        var membersHtml = '<div class="row g-3">';
        d.members.forEach(function (m) {
            membersHtml +=
                '<div class="col-12 col-md-6"><div class="member-card' + (m.isCaptain ? ' cap' : '') + '">' +
                '<span class="av">' + Ui.escapeHtml(m.fullName && m.fullName.length > 0 ? m.fullName.substring(0, 1) : '?') + '</span>' +
                '<div class="grow"><div class="nm">' + Ui.escapeHtml(m.fullName) + '</div><div class="rl">' + Ui.escapeHtml(m.roleLabel) + '</div></div>' +
                (m.isCaptain ? '<span class="badge badge-gold">Đội trưởng</span>' : '') +
                '</div></div>';
        });
        membersHtml += '</div>';

        root.innerHTML =
            '<div class="eyebrow">Trang quản trị đội · Chỉ đội trưởng</div>' +
            '<div class="card-etoms"><div class="body"><div class="team-hero">' + logo +
            '<div style="flex:1;min-width:240px"><h1 style="font-size:1.6rem">' + Ui.escapeHtml(d.name) + ' <span class="text-muted-etoms fw-bold">[' + Ui.escapeHtml(d.tag) + ']</span></h1>' +
            '<div class="sub"><span>' + Ui.escapeHtml(d.gameTitle) + '</span>' + (d.status === 'ACTIVE' ? '<span class="badge badge-active">Đang hoạt động</span>' : '') + '</div></div>' +
            '<a class="btn btn-ghost" href="/team/details.html?id=' + d.teamId + '">Xem hồ sơ công khai</a>' +
            '</div></div>' +
            underStrengthAlert +
            '<div class="row row-cols-2 row-cols-md-3 row-cols-xl-5 g-3 my-4">' +
            '<div class="col"><div class="stat-card h-100"><div class="label">Sĩ số đội</div><div class="num' + (d.isUnderStrength ? '' : ' accent') + '">' + d.activeMemberCount + '/' + d.capacity + '</div><div class="sub">' + d.starterCount + ' chính thức · ' + d.subCount + ' dự bị</div></div></div>' +
            '<div class="col"><div class="stat-card h-100"><div class="label">Giải đang tham gia</div><div class="num">' + d.tournamentCount + '</div><div class="sub">giải đã/đang đăng ký</div></div></div>' +
            '<div class="col"><div class="stat-card h-100"><div class="label">Thẻ phạt</div><div class="num" style="' + (d.penaltyCount > 0 ? 'color:var(--danger)' : '') + '">' + d.penaltyCount + '</div><div class="sub">còn hiệu lực</div></div></div>' +
            '<div class="col"><div class="stat-card h-100"><div class="label">Đơn chờ duyệt</div><div class="num" style="' + (d.pendingCount > 0 ? 'color:var(--warn)' : '') + '">' + d.pendingCount + '</div><div class="sub">ứng tuyển / lời mời</div></div></div>' +
            '<div class="col"><div class="stat-card h-100"><div class="label">Sức chứa theo luật</div><div class="num" style="font-size:1.4rem;margin-top:12px">' + d.capacity + '</div><div class="sub">' + d.maxStarters + ' chính + ' + d.maxSubstitutes + ' dự bị</div></div></div>' +
            '</div>' +
            pendingBanner +
            '<div class="section-title">Thao tác quản trị</div>' +
            '<div class="section-sub">Các chức năng điều hành đội.</div>' +
            '<div class="row row-cols-1 row-cols-sm-2 row-cols-lg-4 g-3">' +
            '<div class="col"><a class="team-card d-block h-100" href="/team/roster.html?id=' + d.teamId + '"><div class="top"><span class="team-logo">R</span><div><div class="name">Quản lý Roster</div><div class="tag">Chính thức / dự bị, loại thành viên</div></div></div><div class="meta"><span>TEAM-03</span><span class="badge badge-active">Mở</span></div></a></div>' +
            '<div class="col"><a class="team-card d-block h-100" href="/team/invites.html?id=' + d.teamId + '"><div class="top"><span class="team-logo">M</span><div><div class="name">Mã mời</div><div class="tag">Tạo link/mã mời thành viên</div></div></div><div class="meta"><span>TEAM-04</span><span class="badge badge-active">Mở</span></div></a></div>' +
            '<div class="col"><a class="team-card d-block h-100" href="/team/roster.html?id=' + d.teamId + '"><div class="top"><span class="team-logo">Đ</span><div><div class="name">Chuyển quyền đội trưởng</div><div class="tag">Trao vai trò cho thành viên khác</div></div></div><div class="meta"><span>TEAM-07</span><span class="badge badge-active">Mở</span></div></a></div>' +
            '<div class="col"><div class="team-card h-100"><div class="top"><span class="team-logo">G</span><div><div class="name">Giải tán đội</div><div class="tag">Xóa mềm, giữ lịch sử thi đấu</div></div></div>' +
            '<div class="meta"><span>TEAM-08</span><button type="button" class="btn btn-ghost" style="padding:6px 12px;color:var(--danger-ink);font-size:.8rem" id="disband-btn">Giải tán đội</button></div></div></div>' +
            '</div>' +
            '<div class="section-title">Thành viên (' + d.activeMemberCount + ')</div>' +
            '<div class="section-sub">Roster hiện tại của đội.</div>' +
            membersHtml +
            '<div class="btn-row mt-4"><a class="btn btn-ghost" href="/team/index.html">← Về danh sách đội</a></div>';

        document.getElementById('disband-btn').addEventListener('click', async function () {
            var ok = await window.etomsConfirm('Giải tán đội ' + d.name + '? Hành động này không thể hoàn tác.');
            if (!ok) return;
            try {
                var res = await Api.del('/api/teams/' + d.teamId);
                Ui.setFlash(res.message, 'success');
                location.href = '/team/index.html';
            } catch (err) {
                Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        Ui.consumeFlash(document.getElementById('flash'));
        var id = Ui.qsInt('id');
        var root = document.getElementById('dash-root');
        if (!id) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã đội</h3></div></div>'; return; }

        var me = await Ui.requireAuth();
        if (!me) return;

        try {
            var d = await Api.get('/api/teams/' + id + '/dashboard');
            render(d);
        } catch (err) {
            if (err.status === 403) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Bạn không phải đội trưởng đội này</h3></div></div>';
            else if (err.status === 404) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy đội</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    });
})();
