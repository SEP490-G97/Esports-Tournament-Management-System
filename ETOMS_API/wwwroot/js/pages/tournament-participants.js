// POR-04: danh sách đội đã duyệt và đội trong danh sách chờ, kèm link hồ sơ đội. Khách xem được.
(function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };

    function teamRow(p, index, waiting) {
        var logo = Ui.safeUrl(p.logoUrl)
            ? '<img src="' + esc(Ui.safeUrl(p.logoUrl)) + '" alt="" />'
            : esc((p.name || '?').trim().slice(0, 1).toUpperCase());
        var rank = waiting
            ? '<span class="badge badge-soft">Chờ #' + esc(p.waitlistPosition || index + 1) + '</span>'
            : p.placementLabel
                ? '<span class="badge badge-gold">' + esc(p.placementLabel) + '</span>'
                : p.finalRank ? '<span class="badge badge-soft">Hạng ' + Number(p.finalRank) + '</span>' : '';
        return '<a class="member-card part-row" href="/team/details.html?id=' + Number(p.teamId) + '">' +
            '<span class="part-no">' + (waiting ? '' : index + 1) + '</span>' +
            '<span class="team-logo">' + logo + '</span>' +
            '<span class="grow"><span class="nm">' + esc(p.name) + '</span><span class="rl">[' + esc(p.tag) + ']</span></span>' +
            rank + '</a>';
    }

    function render(d) {
        document.getElementById('page-title').textContent = 'Đội tham dự · ' + d.tournamentName;
        var approved = d.approved.length
            ? '<div class="d-grid gap-2">' + d.approved.map(function (p, i) { return teamRow(p, i, false); }).join('') + '</div>'
            : '<div class="empty"><h3>Chưa có đội nào được duyệt</h3><p>Danh sách sẽ cập nhật khi Ban tổ chức duyệt đơn đăng ký.</p></div>';
        var waiting = d.waitlisted.length
            ? '<section class="card-etoms mt-3"><div class="body"><div class="section-title mt-0">Danh sách chờ (' + d.waitlisted.length + ')</div>' +
              '<p class="hint">Khi có đội rút đơn, đội đứng đầu danh sách chờ được mời nhận suất.</p>' +
              '<div class="d-grid gap-2">' + d.waitlisted.map(function (p, i) { return teamRow(p, i, true); }).join('') + '</div></div></section>'
            : '';
        document.getElementById('participants-root').innerHTML =
            '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Đội đã duyệt (' + d.approved.length + '/' + d.maxTeams + ')</div>' +
            approved + '</div></section>' + waiting;
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var id = Ui.qsInt('id');
        var root = document.getElementById('participants-root');
        if (!id || id < 1) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã giải</h3><p>Hãy mở giải từ <a href="/tournament/index.html">danh sách giải</a>.</p></div></div>';
            return;
        }
        try {
            render((await Api.get('/api/tournaments/' + id + '/participants')).data);
        } catch (err) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>' +
                (err.status === 404 ? 'Không tìm thấy giải đấu' : 'Không tải được danh sách đội') + '</h3><p>' +
                esc(err.message || '') + '</p><a class="btn btn-primary mt-3" href="/tournament/index.html">Về danh sách giải</a></div></div>';
        }
    });
})();
