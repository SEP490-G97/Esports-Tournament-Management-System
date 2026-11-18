// Trang Tin tức: tin ETOMS (Admin đăng) + tin esports từ RSS các báo (NewsController / NewsFeedService).
// Tin báo chỉ có tiêu đề, mô tả ngắn, ảnh do RSS cung cấp; bấm vào mở bài gốc trên trang báo.
(function () {
    var state = { source: 'all', q: '', page: 1, pageSize: 12, items: [], total: 0 };
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var el = function (id) { return document.getElementById(id); };

    function fmtDate(iso) {
        if (!iso) return '';
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? '' : d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    function card(n) {
        var url = n.isInternal ? n.url : Ui.safeUrl(n.url);
        if (!url) return '';
        var img = Ui.safeUrl(n.imageUrl);
        var external = !n.isInternal;
        return '<a class="news-card" href="' + esc(url) + '"' + (external ? ' target="_blank" rel="noopener noreferrer nofollow"' : '') + '>' +
            '<div class="news-card-img"' + (img ? ' style="background-image:url(\'' + esc(img).replace(/'/g, '%27') + '\')"' : '') + '>' +
            (img ? '' : '<span>' + esc(n.sourceName) + '</span>') + '</div>' +
            '<div class="news-card-body">' +
            '<div class="news-card-meta"><span class="badge ' + (n.isInternal ? 'badge-active' : 'badge-soft') + '">' + esc(n.sourceName) + '</span>' +
            (n.isPinned ? '<span class="badge badge-gold">Ghim</span>' : '') + '<span>' + esc(fmtDate(n.publishedAt)) + '</span></div>' +
            '<h3>' + esc(n.title) + '</h3>' +
            (n.summary ? '<p>' + esc(n.summary) + '</p>' : '') +
            '<span class="news-card-more">' + (external ? 'Đọc tại ' + esc(n.sourceName) + ' ↗' : 'Đọc tin') + '</span>' +
            '</div></a>';
    }

    function renderSources(sources) {
        var chips = [{ key: 'all', name: 'Tất cả', ok: true }].concat(sources);
        el('news-sources').innerHTML = chips.map(function (s) {
            var on = s.key === state.source;
            return '<button type="button" role="tab" aria-selected="' + on + '" class="news-chip' + (on ? ' active' : '') + '" data-source="' + esc(s.key) + '">' +
                esc(s.name) + (s.key !== 'all' && s.count !== undefined ? ' <small>' + Number(s.count) + '</small>' : '') + '</button>';
        }).join('');
        el('news-sources').querySelectorAll('[data-source]').forEach(function (b) {
            b.addEventListener('click', function () { state.source = b.dataset.source; load(true); });
        });
        var down = sources.filter(function (s) { return !s.ok; }).map(function (s) { return s.name; });
        el('news-note').textContent = (down.length ? 'Tạm chưa lấy được tin từ: ' + down.join(', ') + '. ' : '') +
            'Tin báo tự cập nhật khoảng 30 phút một lần; bản quyền nội dung thuộc về báo gốc.';
    }

    async function load(reset) {
        if (reset) { state.page = 1; state.items = []; }
        Ui.clearAlert(el('news-flash'));
        if (reset) el('news-list').innerHTML = '<div class="card-etoms"><div class="empty"><h3>Đang tải tin…</h3></div></div>';
        try {
            var qs = new URLSearchParams({ source: state.source, page: state.page, pageSize: state.pageSize });
            if (state.q) qs.set('q', state.q);
            var res = await Api.get('/api/news?' + qs.toString());
            state.total = res.total;
            state.items = state.items.concat(res.items || []);
            renderSources(res.sources || []);
            el('news-list').innerHTML = state.items.length ? state.items.map(card).join('')
                : '<div class="card-etoms"><div class="empty"><h3>Chưa có tin phù hợp</h3><p>Thử nguồn khác hoặc từ khóa khác.</p></div></div>';
            el('news-more').hidden = state.items.length >= state.total;
        } catch (err) {
            el('news-list').innerHTML = '';
            Ui.showAlert(el('news-flash'), 'danger', err.message || 'Không tải được tin tức.');
        }
    }

    // ?id= : đọc một bài tin ETOMS ngay trên trang.
    async function openArticle(id) {
        var box = el('news-article');
        try {
            var a = await Api.get('/api/news/' + id);
            var body = /<[a-z][\s\S]*>/i.test(a.body || '') ? a.body
                : (a.body || '').split(/\n{2,}|\r\n\r\n/).map(function (p) { return '<p>' + esc(p).replace(/\n/g, '<br>') + '</p>'; }).join('');
            box.innerHTML = '<div class="body"><div class="news-card-meta mb-2"><span class="badge badge-active">ETOMS</span><span>' + esc(fmtDate(a.publishedAt)) + '</span>' +
                (a.author ? '<span>' + esc(a.author) + '</span>' : '') + '</div><h2 class="mb-3">' + esc(a.title) + '</h2>' +
                '<div class="rulebook-content">' + body + '</div><a class="btn btn-ghost mt-3" href="/news/index.html">← Tất cả tin</a></div>';
            box.hidden = false;
            document.title = a.title + ' · ETOMS';
        } catch (err) {
            box.innerHTML = '<div class="empty"><h3>Không tìm thấy bài tin</h3><p>' + esc(err.message || '') + '</p></div>';
            box.hidden = false;
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        var id = Ui.qsInt('id');
        if (id) openArticle(id);
        var timer;
        el('news-search').addEventListener('input', function () {
            clearTimeout(timer);
            timer = setTimeout(function () { state.q = el('news-search').value.trim(); load(true); }, 350);
        });
        el('news-more').addEventListener('click', function () { state.page++; load(false); });
        load(true);
    });
})();
