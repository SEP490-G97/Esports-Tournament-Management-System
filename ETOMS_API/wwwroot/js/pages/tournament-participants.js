// POR-04: đội tham dự (đội chính thức — kể cả đội bị loại giữa giải) và đội trong danh sách chờ, kèm link hồ sơ đội. Khách xem được.
(function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };

    function teamRow(p, index, waiting) {
        var logo = Ui.safeUrl(p.logoUrl)
            ? '<img src="' + esc(Ui.safeUrl(p.logoUrl)) + '" alt="" />'
            : esc((p.name || '?').trim().slice(0, 1).toUpperCase());
        var rank = waiting
            ? '<span class="badge badge-soft">Chờ suất #' + esc(p.waitlistPosition || index + 1) + '</span>'
            : p.disqualified ? '<span class="badge badge-danger">Bị loại</span>'
            : p.placementLabel ? '<span class="badge badge-gold">' + esc(p.placementLabel) + '</span>'
            : p.finalRank ? '<span class="badge badge-soft">Hạng ' + Number(p.finalRank) + '</span>' : '';
        return '<a class="member-card part-row' + (p.disqualified ? ' is-out' : '') + '" href="/team/details.html?id=' + Number(p.teamId) + '">' +
            '<span class="part-no">' + (waiting ? '' : index + 1) + '</span>' +
            '<span class="team-logo">' + logo + '</span>' +
            '<span class="grow"><span class="nm">' + esc(p.name) + '</span><span class="rl">[' + esc(p.tag) + ']</span></span>' +
            rank + '</a>';
    }

    function emptyText(status) {
        if (status === 'CANCELLED') return ['Giải đã bị hủy', 'Giải không diễn ra nên không có đội tham dự.'];
        return ['Chưa có đội tham dự', 'Đội nào được Ban tổ chức duyệt đơn đăng ký sẽ xuất hiện ở đây.'];
    }

    function render(d) {
        document.getElementById('page-title').textContent = 'Đội tham dự · ' + d.tournamentName;
        var ranked = d.status === 'COMPLETED' && d.approved.some(function (p) { return p.finalRank; });
        var empty = emptyText(d.status);
        var approved = d.approved.length
            ? '<div data-approved></div>'
            : '<div class="empty"><h3>' + esc(empty[0]) + '</h3><p>' + esc(empty[1]) + '</p></div>';
        var waiting = d.waitlisted.length
            ? '<section class="card-etoms mt-3"><div class="body"><div class="section-title mt-0">Danh sách chờ (' + d.waitlisted.length + ' đội)</div>' +
              '<p class="hint">Giải đã đủ suất. Khi có đội rút đơn, đội đứng đầu danh sách chờ được mời nhận suất.</p>' +
              '<div data-waiting></div></div></section>'
            : '';
        var root = document.getElementById('participants-root');
        root.innerHTML =
            '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Đội tham dự <span class="hint">(' + d.approved.length + '/' + d.maxTeams + ' suất)</span></div>' +
            (ranked ? '<p class="hint">Xếp theo thứ hạng chung cuộc.</p>' : '') +
            approved + '</div></section>' + waiting;
        // Giải nhiều đội (64–128): tìm theo tên/tag + phân trang (góp ý 27/11). Giữ số thứ tự gốc khi lọc/sang trang.
        pageTeams(root.querySelector('[data-approved]'), d.approved, false);
        pageTeams(root.querySelector('[data-waiting]'), d.waitlisted, true);
    }

    function pageTeams(el, list, waiting) {
        if (!el) return;
        Ui.listPager(el, {
            items: list.map(function (p, i) { return { p: p, i: i }; }), pageSize: 10, listClass: 'd-grid gap-2',
            render: function (x) { return teamRow(x.p, x.i, waiting); },
            text: function (x) { return x.p.name + ' ' + x.p.tag; },
            placeholder: 'Tìm đội theo tên hoặc tag...'
        });
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
