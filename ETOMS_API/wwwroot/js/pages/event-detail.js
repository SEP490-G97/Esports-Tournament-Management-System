// Trang sự kiện / quà tặng do Admin đăng (/events/index.html?id=) — nội dung đọc ngay trong ETOMS, không dẫn ra trang ngoài.
(function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var TYPE = { EVENT: 'Sự kiện', GIVEAWAY: 'Quà tặng' };

    function fmt(iso) {
        var d = new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? '—' : d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    function empty(title, text) {
        document.getElementById('event-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>' + esc(title) + '</h3><p>' + esc(text) +
            '</p><a class="btn btn-ghost mt-3" href="/">Về trang chủ</a></div></div>';
    }

    function render(e) {
        document.title = e.title + ' · ETOMS';
        // Mô tả: mỗi dòng trống là một đoạn.
        var paras = String(e.description || '').split(/\n\s*\n/).map(function (p) { return p.trim(); }).filter(Boolean);
        var link = e.linkUrl && /^\/(?!\/)/.test(e.linkUrl) ? e.linkUrl : null;
        document.getElementById('event-root').innerHTML =
            '<article class="card-etoms event-article">' +
            '<div class="event-cover" role="img" aria-label="' + esc(e.title) + '" style="background-image:url(\'' + esc(Ui.safeUrl(e.imageUrl)) + '\')"></div>' +
            '<div class="body">' +
            '<span class="badge badge-gold">' + esc(TYPE[e.bannerType] || e.bannerType) + '</span>' +
            '<h1 class="event-title">' + esc(e.title) + '</h1>' +
            '<p class="event-time">Diễn ra từ <b>' + esc(fmt(e.startAt)) + '</b> đến <b>' + esc(fmt(e.endAt)) + '</b></p>' +
            (paras.length ? paras.map(function (p) { return '<p class="event-text">' + esc(p) + '</p>'; }).join('') : '<p class="event-text">Ban quản trị ETOMS chưa ghi thêm nội dung cho sự kiện này.</p>') +
            '<div class="d-flex flex-wrap gap-2 mt-3">' +
            (link ? '<a class="btn btn-primary" href="' + esc(link) + '">Xem chi tiết</a>' : '') +
            '<a class="btn btn-ghost" href="/">Về trang chủ</a></div>' +
            '</div></article>';
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var id = Ui.qsInt('id');
        if (id === null) return empty('Không xác định được sự kiện', 'Đường dẫn thiếu mã sự kiện.');
        try { render(await Api.get('/api/home/banners/' + id)); }
        catch (err) { empty('Không xem được sự kiện', err.status === 404 ? 'Sự kiện không tồn tại, đã kết thúc hoặc chưa bắt đầu.' : (err.message || 'Lỗi tải dữ liệu.')); }
    });
})();
