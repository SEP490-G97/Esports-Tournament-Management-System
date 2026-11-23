// POR-02 / UC-36: danh sách & tìm kiếm giải — khách xem được. Bộ lọc nằm trên URL nên
// "Quay lại" và chia sẻ link vẫn giữ nguyên bộ lọc (UC-36 E1: giữ bộ lọc để nới rộng).
(function () {
    var FIELDS = ['q', 'gameId', 'state', 'from', 'to', 'region', 'fee', 'sort'];
    var PAGE_SIZE = 12;
    var form, flash;

    function el(id) { return document.getElementById(id); }

    function fmtDate(iso) {
        if (!iso) return '';
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? '' : d.toLocaleDateString('vi-VN');
    }

    function fmtFee(v) {
        return Number(v) > 0 ? Number(v).toLocaleString('vi-VN') + 'đ' : 'Miễn phí';
    }

    // Đọc bộ lọc từ URL vào form.
    function readUrl() {
        var p = new URLSearchParams(location.search);
        FIELDS.forEach(function (name) {
            var input = form.elements[name];
            if (input) input.value = p.get(name) || (name === 'sort' ? 'RELEVANT' : '');
        });
        var page = parseInt(p.get('page'), 10);
        return isNaN(page) || page < 1 ? 1 : page;
    }

    // Kiểm tra phía trình duyệt — cùng luật với TournamentDirectoryService.
    function validate() {
        var errors = {};
        var q = form.elements.q.value.trim();
        var from = form.elements.from.value, to = form.elements.to.value;
        if (q.length > 100) errors.Q = 'Từ khóa tìm kiếm tối đa 100 ký tự.';
        if (from && to && from > to) errors.To = 'Đến ngày phải sau hoặc bằng Từ ngày.';
        Ui.clearFieldErrors(form);
        Ui.applyFieldErrors(form, errors);
        return Object.keys(errors).length === 0;
    }

    function currentParams(page) {
        var p = new URLSearchParams();
        FIELDS.forEach(function (name) {
            var v = (form.elements[name].value || '').trim();
            if (v && !(name === 'sort' && v === 'RELEVANT')) p.set(name, v);
        });
        if (page > 1) p.set('page', String(page));
        return p;
    }

    function card(t) {
        var pct = t.maxTeams > 0 ? Math.min(100, Math.floor(t.teams * 100 / t.maxTeams)) : 0;
        var place = t.isOnline ? 'Trực tuyến' : (t.region || 'Chưa rõ khu vực');
        return '<div class="col-12 col-md-6 col-lg-4 d-flex flex-column">' +
            '<a class="feat-card d-block flex-grow-1" href="/bracket/view.html?id=' + Number(t.tournamentId) + '" style="color:inherit">' +
            '<div class="feat-cover" style="background-image:linear-gradient(180deg, rgba(10,14,23,.2), rgba(10,14,23,.8)), url(\'' + Ui.escapeHtml(Ui.safeUrl(t.imageUrl) || '/images/tournament-cover.svg') + '\')">' +
            '<span class="badge ' + Ui.escapeHtml(t.statusClass) + '">' + Ui.escapeHtml(t.statusLabel) + '</span>' +
            '<span class="feat-game">' + Ui.escapeHtml(t.game) + '</span></div>' +
            '<div class="feat-body">' +
            '<div class="feat-name">' + Ui.escapeHtml(t.name) + '</div>' +
            '<div class="hint mb-2">' + Ui.escapeHtml(t.organizingUnit || 'Chưa ghi đơn vị tổ chức') + ' · ' + Ui.escapeHtml(t.formatLabel) + '</div>' +
            '<div class="feat-meta">' +
            '<div><span class="k">Khai mạc</span><span class="v">' + Ui.escapeHtml(fmtDate(t.startAt)) + '</span></div>' +
            '<div><span class="k">Lệ phí</span><span class="v">' + Ui.escapeHtml(fmtFee(t.entryFee)) + '</span></div>' +
            '<div><span class="k">Số đội</span><span class="v">' + Number(t.teams) + '/' + Number(t.maxTeams) + '</span></div>' +
            '</div>' +
            '<div class="hint mt-2">' + Ui.escapeHtml(place) + (t.registrationOpen ? ' · Hạn đăng ký ' + Ui.escapeHtml(fmtDate(t.regCloseAt)) : '') + '</div>' +
            '<div class="feat-bar"><span style="width:' + pct + '%"></span></div>' +
            // Giống thẻ giải ở trang chủ (góp ý nhóm): không có nút "Đăng ký cho đội" dưới thẻ — bấm thẻ vào trang giải, đăng ký ở đó.
            // Trước đây nút chỉ hiện dưới giải đang mở đăng ký nên các thẻ lệch hàng, lọc theo game trông như chỉ có một nút lạc.
            '</div></a></div>';
    }

    function renderPager(page, totalPages) {
        var nav = el('dir-pager');
        if (totalPages <= 1) { nav.innerHTML = ''; return; }
        var html = '';
        function btn(p, label, disabled, active) {
            return '<button type="button" class="btn ' + (active ? 'btn-primary' : 'btn-ghost') + ' btn-sm" data-page="' + p + '"' +
                (disabled ? ' disabled' : '') + (active ? ' aria-current="page"' : '') + '>' + label + '</button>';
        }
        html += btn(page - 1, '‹ Trước', page <= 1, false);
        for (var p = 1; p <= totalPages; p++) {
            if (p === 1 || p === totalPages || Math.abs(p - page) <= 1) html += btn(p, String(p), false, p === page);
            else if (Math.abs(p - page) === 2) html += '<span class="hint px-1">…</span>';
        }
        html += btn(page + 1, 'Sau ›', page >= totalPages, false);
        nav.innerHTML = html;
        nav.querySelectorAll('[data-page]').forEach(function (b) {
            b.addEventListener('click', function () { search(parseInt(b.dataset.page, 10), true); });
        });
    }

    async function search(page, pushState) {
        Ui.clearAlert(flash);
        if (!validate()) {
            el('dir-list').innerHTML = '<div class="card-etoms"><div class="empty"><h3>Bộ lọc chưa hợp lệ</h3><p>Sửa các ô được đánh dấu đỏ rồi bấm "Lọc".</p></div></div>';
            el('dir-count').textContent = '';
            el('dir-pager').innerHTML = '';
            return;
        }
        var params = currentParams(page);
        if (pushState) history.pushState(null, '', location.pathname + (params.toString() ? '?' + params : ''));
        params.set('pageSize', String(PAGE_SIZE));
        if (!params.has('page')) params.set('page', '1');

        var list = el('dir-list');
        list.innerHTML = '<div class="card-etoms"><div class="empty is-loading"><h3>Đang tải danh sách giải...</h3></div></div>';
        try {
            var res = await Api.get('/api/tournaments?' + params.toString());
            var data = res.data;
            el('dir-count').textContent = data.total > 0
                ? 'Tìm thấy ' + data.total + ' giải · trang ' + data.page + '/' + data.totalPages : '';
            if (!data.items.length) {
                // UC-36 E1: không có giải phù hợp — giữ bộ lọc để người xem nới rộng.
                list.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không có giải đấu phù hợp</h3>' +
                    '<p>Bộ lọc vẫn được giữ — hãy bỏ bớt điều kiện hoặc bấm "Xóa lọc".</p></div></div>';
                renderPager(1, 1);
                return;
            }
            list.innerHTML = '<div class="row g-4">' + data.items.map(card).join('') + '</div>';
            renderPager(data.page, data.totalPages);
        } catch (err) {
            Ui.applyFieldErrors(form, err.fieldErrors);
            list.innerHTML = '';
            el('dir-count').textContent = '';
            Ui.showAlert(flash, 'danger', err.message || 'Không tải được danh sách giải.');
        }
    }

    async function loadFilters() {
        try {
            var res = await Api.get('/api/tournaments/filters');
            var d = res.data;
            el('f-game').innerHTML = '<option value="">Tất cả tựa game</option>' + d.games.map(function (g) {
                return '<option value="' + g.gameId + '">' + Ui.escapeHtml(g.title) + '</option>';
            }).join('');
            el('f-region').innerHTML = '<option value="">Tất cả khu vực</option>' +
                (d.hasOnline ? '<option value="ONLINE">Trực tuyến</option>' : '') +
                d.regions.map(function (r) { return '<option value="' + Ui.escapeHtml(r) + '">' + Ui.escapeHtml(r) + '</option>'; }).join('');
        } catch (e) { /* ô lọc vẫn dùng được với giá trị mặc định */ }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        form = el('dir-filters');
        flash = el('flash');
        await loadFilters();
        var page = readUrl();

        // Lọc ngay khi đổi ô chọn / ngày; ô tìm kiếm lọc sau khi ngừng gõ (Ui.autoFilter) — không cần nút "Lọc".
        Ui.autoFilter(form, function () { search(1, true); });
        el('dir-reset').addEventListener('click', function () {
            form.reset();
            Ui.clearFieldErrors(form);
            search(1, true);
        });
        window.addEventListener('popstate', function () { search(readUrl(), false); });

        search(page, false);
    });
})();
