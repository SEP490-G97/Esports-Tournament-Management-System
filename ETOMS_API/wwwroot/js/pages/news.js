// Trang Tin tức: tin ETOMS (Admin đăng) + tin esports từ RSS các báo (NewsController / NewsFeedService).
// Bấm tin nào cũng mở TRANG ĐỌC RIÊNG trên ETOMS (?id= tin ETOMS, ?ext= tin báo). Tin báo chỉ có tiêu đề, ảnh, tóm tắt do
// báo cung cấp qua RSS + nút đọc toàn bài trên trang báo (toàn văn thuộc bản quyền báo gốc, không chép về).
// Lọc theo game (?game=VAL, banner đổi theo game), nguồn, khoảng ngày và từ khóa; phân trang số.
(function () {
    // 11 tin/trang: tin đầu là thẻ lớn chiếm 2 ô → đúng 12 ô, chia đều cho lưới 2, 3 hoặc 4 cột (không còn ô lẻ).
    var state = { source: 'all', game: 'all', range: 'all', from: '', to: '', q: '', page: 1, pageSize: 11, total: 0, reading: false };
    var games = [];
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var el = function (id) { return document.getElementById(id); };

    // Màu chủ đạo từng game cho banner khi game chưa có ảnh nền riêng.
    var ACCENT = { LOL: '#c8aa6e', VAL: '#ff4655', LQM: '#f5b51b', PUM: '#f2a900', LMTC: '#3fb4ff', FCO: '#00d26a' };

    function fmtDate(iso) {
        var d = Ui.parseUtc ? Ui.parseUtc(iso) : (iso ? new Date(/Z$/.test(iso) ? iso : iso + 'Z') : null);
        return d && !isNaN(d.getTime()) ? d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : '';
    }
    function ymd(d) { return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
    function gameOf(code) { return games.filter(function (g) { return g.code === code; })[0] || null; }
    function cssUrl(u) { return "url('" + String(u).replace(/'/g, '%27') + "')"; }

    function card(n, lead) {
        var url = n.readerUrl;
        if (!url) return '';
        var img = Ui.safeUrl(n.imageUrl);
        var g = gameOf(n.gameCode);
        var cover = img ? '<div class="news-card-img" style="background-image:' + esc(cssUrl(img)) + '"></div>'
            : '<div class="news-card-img news-card-ph"' + (g ? ' style="--ph-accent:' + (ACCENT[g.code] || 'var(--primary)') + '"' : '') + '>' +
              (g && g.logoUrl ? '<img src="' + esc(g.logoUrl) + '" alt="" loading="lazy">' : '<span>ETOMS</span>') + '</div>';
        return '<a class="news-card' + (lead ? ' news-card-lead' : '') + '" href="' + esc(url) + '">' + cover +
            '<div class="news-card-body">' +
            '<div class="news-card-meta"><span class="badge ' + (n.isInternal ? 'badge-active' : 'badge-soft') + '">' + esc(n.sourceName) + '</span>' +
            (g ? '<span class="badge badge-soft">' + esc(g.title) + '</span>' : '') +
            (n.isPinned ? '<span class="badge badge-gold">Ghim</span>' : '') + '<span>' + esc(fmtDate(n.publishedAt)) + '</span></div>' +
            '<h3>' + esc(n.title) + '</h3>' +
            (n.summary ? '<p>' + esc(n.summary) + '</p>' : '') +
            '<span class="news-card-more">Đọc tin →</span>' +
            '</div></a>';
    }

    // Ảnh nền banner theo game, phải NÉT (góp ý 23/11: nền nhòe "mờ tịt"): 1) ảnh banner riêng của game (Game.BannerUrl /
    // images/games/banners/<mã>.jpg); 2) ảnh bài tin mới nhất về game đó (ảnh thật, đổi theo tin); 3) game chưa có tin →
    // logo nét đặt bên phải trên nền màu của game. Nhớ ảnh theo game để sang trang 2, 3 không đổi nền.
    var heroPhoto = {};
    function renderHero(items) {
        var g = gameOf(state.game);
        var hero = el('news-hero'), bg = el('news-hero-bg'), logo = el('news-hero-logo');
        hero.style.setProperty('--news-accent', g ? (ACCENT[g.code] || '#7c5cff') : '#7c5cff');
        var key = g ? g.code : 'all';
        if (!heroPhoto[key] && g) {
            var withImg = (items || []).filter(function (n) { return n.gameCode === g.code && Ui.safeUrl(n.imageUrl); })[0];
            if (withImg) heroPhoto[key] = Ui.safeUrl(withImg.imageUrl);
        }
        var photo = g ? (g.bannerUrl || heroPhoto[key]) : '/images/hero.jpg';
        hero.classList.toggle('is-logo', !photo);
        bg.style.backgroundImage = cssUrl(photo || (g && g.logoUrl) || '/images/hero.jpg');
        if (g && g.logoUrl) { logo.src = g.logoUrl; logo.hidden = false; } else { logo.hidden = true; logo.removeAttribute('src'); }
        el('news-hero-title').textContent = g ? 'Tin tức ' + g.title : 'Tin tức esports';
        el('news-hero-sub').textContent = g ? 'Tin mới nhất về ' + g.title + ' từ ETOMS và các trang báo thể thao điện tử.'
            : 'Tin của ETOMS và tin thể thao điện tử mới nhất từ các trang báo.';
        el('news-games').innerHTML = [{ code: 'all', title: 'Tất cả' }].concat(games).map(function (x) {
            var on = x.code === state.game;
            return '<button type="button" class="news-game-tab' + (on ? ' active' : '') + '" data-game="' + esc(x.code) + '" aria-pressed="' + on + '">' +
                (x.logoUrl ? '<img src="' + esc(x.logoUrl) + '" alt="" loading="lazy">' : '') + esc(x.title) +
                (x.code !== 'all' && x.count ? ' <small>' + Number(x.count) + '</small>' : '') + '</button>';
        }).join('');
    }

    function renderSources(sources) {
        var sel = el('news-source');
        sel.innerHTML = '<option value="all">Tất cả nguồn</option>' + sources.map(function (s) {
            return '<option value="' + esc(s.key) + '"' + (s.key === state.source ? ' selected' : '') + '>' + esc(s.name) + (s.ok ? '' : ' (tạm lỗi)') + '</option>';
        }).join('');
        var down = sources.filter(function (s) { return !s.ok; }).map(function (s) { return s.name; });
        el('news-note').textContent = (down.length ? 'Tạm chưa lấy được tin từ: ' + down.join(', ') + '. ' : '') +
            'Tin báo tự cập nhật khoảng 30 phút một lần; bản quyền nội dung thuộc về báo gốc.';
    }

    function filtersActive() { return state.source !== 'all' || state.range !== 'all' || state.q !== ''; }

    function syncUrl() {
        var p = new URLSearchParams(location.search);
        if (state.game === 'all') p.delete('game'); else p.set('game', state.game);
        var s = p.toString();
        history.replaceState(null, '', location.pathname + (s ? '?' + s : ''));
    }

    async function load() {
        Ui.clearAlert(el('news-flash'));
        el('news-list').innerHTML = '<div class="card-etoms"><div class="empty"><h3>Đang tải tin…</h3></div></div>';
        try {
            var qs = new URLSearchParams({ source: state.source, game: state.game, page: state.page, pageSize: state.pageSize });
            if (state.q) qs.set('q', state.q);
            if (state.from) qs.set('from', state.from);
            if (state.to) qs.set('to', state.to);
            var res = await Api.get('/api/news?' + qs.toString());
            games = res.games || [];
            state.total = res.total;
            renderHero(res.items);
            renderSources(res.sources || []);
            var items = res.items || [];
            var lead = items.length > 1;
            el('news-list').innerHTML = items.length ? items.map(function (n, i) { return card(n, lead && i === 0); }).join('')
                : '<div class="card-etoms news-empty"><div class="empty"><h3>Chưa có tin phù hợp</h3><p>Thử game, nguồn, khoảng ngày hoặc từ khóa khác.</p></div></div>';
            var pages = Math.max(1, Math.ceil(state.total / state.pageSize));
            el('news-count').textContent = state.total ? state.total + ' tin' + (pages > 1 ? ' · trang ' + state.page + '/' + pages : '') : '';
            el('news-pager').innerHTML = Ui.pagerHtml(state.page, pages);
            el('news-reset').hidden = !filtersActive();
        } catch (err) {
            el('news-list').innerHTML = '';
            Ui.showAlert(el('news-flash'), 'danger', err.message || 'Không tải được tin tức.');
        }
    }
    function reload() { state.page = 1; syncUrl(); load(); }

    function setRange(range) {
        state.range = range;
        document.querySelectorAll('[data-range]').forEach(function (b) { b.classList.toggle('active', b.dataset.range === range); });
        el('news-custom-range').hidden = range !== 'custom';
        var today = new Date();
        if (range === 'all') { state.from = state.to = ''; }
        else if (range === 'today') { state.from = state.to = ymd(today); }
        else if (range === '7' || range === '30') {
            var start = new Date(today); start.setDate(start.getDate() - (Number(range) - 1));
            state.from = ymd(start); state.to = ymd(today);
        } else {
            state.from = el('news-from').value; state.to = el('news-to').value;
        }
    }

    // Ô ngày: không chọn được ngày tương lai, "đến ngày" không trước "từ ngày" (báo ngay khi chọn).
    function onCustomDate() {
        var f = el('news-from'), t = el('news-to');
        t.min = f.value || '';
        f.max = t.value || ymd(new Date());
        if (f.value && t.value && f.value > t.value) {
            Ui.showAlert(el('news-flash'), 'warning', 'Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.');
            return;
        }
        setRange('custom');
        reload();
    }

    function relatedCards(list) {
        return (list || []).map(function (r) { return card(r, false); }).join('');
    }

    // Trang đọc tin (góp ý 27/11): mở một tin là sang trang đọc riêng — ẩn banner, bộ lọc, ô tìm và danh sách,
    // chỉ còn bài đọc + "Tin khác". Đường dẫn: "Tin tức › <game>" để quay lại danh sách.
    function crumb(gameCode) {
        var g = gameOf(gameCode);
        return '<nav class="news-crumb" aria-label="Đường dẫn"><a href="/news/index.html">Tin tức</a>' +
            (g ? '<span aria-hidden="true">›</span><a href="/news/index.html?game=' + encodeURIComponent(g.code) + '">' + esc(g.title) + '</a>' : '') + '</nav>';
    }
    function relatedBlock(list, gameCode) {
        var html = relatedCards(list);
        var g = gameOf(gameCode);
        return html ? '<div class="news-related-block"><h3 class="section-title">Tin khác' + (g ? ' về ' + esc(g.title) : '') + '</h3>' +
            '<div class="news-cards news-cards-compact">' + html + '</div></div>' : '';
    }

    // ?id= : đọc một bài tin ETOMS.
    async function openArticle(id, latest) {
        var box = el('news-article');
        state.reading = true;
        box.innerHTML = '<div class="empty"><h3>Đang tải tin…</h3></div>';
        box.hidden = false;
        try {
            var a = await Api.get('/api/news/' + id);
            var body = /<[a-z][\s\S]*>/i.test(a.body || '') ? a.body
                : (a.body || '').split(/\n{2,}|\r\n\r\n/).map(function (p) { return '<p>' + esc(p).replace(/\n/g, '<br>') + '</p>'; }).join('');
            var others = (latest || []).filter(function (n) { return n.readerUrl !== '/news/index.html?id=' + id; }).slice(0, 6);
            box.innerHTML = crumb(a.gameCode) + '<div class="body news-reader"><div class="news-card-meta mb-2"><span class="badge badge-active">ETOMS</span><span>' + esc(fmtDate(a.publishedAt)) + '</span>' +
                (a.author ? '<span>' + esc(a.author) + '</span>' : '') + '</div><h1 class="news-reader-title">' + esc(a.title) + '</h1>' +
                '<div class="rulebook-content">' + body + '</div><a class="btn btn-ghost mt-3" href="/news/index.html">← Tất cả tin</a></div>' +
                relatedBlock(others, null);
            document.title = a.title + ' · ETOMS';
        } catch (err) {
            box.innerHTML = '<div class="empty"><h3>Không tìm thấy bài tin</h3><p>' + esc(err.message || '') + '</p><a class="btn btn-ghost" href="/news/index.html">← Tất cả tin</a></div>';
        }
    }

    // ?ext= : đọc một tin báo trên ETOMS — tóm tắt do báo cung cấp, toàn văn đọc ở báo gốc.
    async function openExternal(id) {
        var box = el('news-article');
        state.reading = true;
        box.innerHTML = '<div class="empty"><h3>Đang tải tin…</h3></div>';
        box.hidden = false;
        try {
            var a = await Api.get('/api/news/external/' + encodeURIComponent(id));
            var src = Ui.safeUrl(a.url);
            var img = Ui.safeUrl(a.imageUrl);
            var paras = (a.description || '').split(/(?<=[.!?…])\s+(?=[A-ZĐÂĂÊÔƠƯÁÀẢÃẠ0-9"“])/).reduce(function (acc, s) {
                // gộp câu thành đoạn ~3 câu cho dễ đọc
                if (!acc.length || acc[acc.length - 1].split(/[.!?…]\s/).length >= 3) acc.push(s); else acc[acc.length - 1] += ' ' + s;
                return acc;
            }, []);
            var g = gameOf(a.gameCode);
            var srcHost = '';
            try { srcHost = src ? new URL(src).hostname.replace(/^www\./, '') : ''; } catch (e) { srcHost = ''; }
            box.innerHTML = crumb(a.gameCode) + '<div class="body news-reader">' +
                '<div class="news-card-meta mb-2"><span class="badge badge-soft">' + esc(a.sourceName) + '</span>' +
                (g ? '<span class="badge badge-soft">' + esc(g.title) + '</span>' : '') + '<span>' + esc(fmtDate(a.publishedAt)) + '</span></div>' +
                '<h1 class="news-reader-title">' + esc(a.title) + '</h1>' +
                (img ? '<img class="news-reader-img" src="' + esc(img) + '" alt="" loading="lazy" referrerpolicy="no-referrer">' : '') +
                '<section class="news-summary" aria-labelledby="news-summary-title">' +
                '<h2 id="news-summary-title" class="news-summary-title">Tóm tắt</h2>' +
                '<div class="rulebook-content">' + (paras.length ? paras.map(function (p) { return '<p>' + esc(p) + '</p>'; }).join('') : '<p>Báo chưa cung cấp đoạn tóm tắt cho tin này.</p>') + '</div>' +
                '<dl class="news-facts">' +
                '<div><dt>Nguồn</dt><dd>' + esc(a.sourceName) + (srcHost ? ' (' + esc(srcHost) + ')' : '') + '</dd></div>' +
                (fmtDate(a.publishedAt) ? '<div><dt>Đăng lúc</dt><dd>' + esc(fmtDate(a.publishedAt)) + '</dd></div>' : '') +
                (g ? '<div><dt>Tựa game</dt><dd>' + esc(g.title) + '</dd></div>' : '') +
                '</dl></section>' +
                '<p class="field-hint">Tóm tắt do báo ' + esc(a.sourceName) + ' cung cấp. Toàn văn bài viết thuộc bản quyền của báo ' + esc(a.sourceName) + ', bấm nút dưới để đọc đầy đủ.</p>' +
                '<div class="btn-row mt-2">' +
                (src ? '<a class="btn btn-primary" href="' + esc(src) + '" target="_blank" rel="noopener noreferrer nofollow">Đọc toàn bài tại ' + esc(a.sourceName) + ' ↗</a>' : '') +
                '<a class="btn btn-ghost" href="/news/index.html' + (g ? '?game=' + encodeURIComponent(g.code) : '') + '">← Tất cả tin</a></div>' +
                '</div>' +
                relatedBlock(a.related, a.gameCode);
            document.title = a.title + ' · ETOMS';
            window.scrollTo(0, 0);
        } catch (err) {
            box.innerHTML = '<div class="empty"><h3>Không mở được tin</h3><p>' + esc(err.message || '') + '</p><a class="btn btn-ghost" href="/news/index.html">← Tất cả tin</a></div>';
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        var params = new URLSearchParams(location.search);
        var g = (params.get('game') || '').toUpperCase();
        if (/^[A-Z]{2,8}$/.test(g)) state.game = g;
        el('news-from').max = el('news-to').max = ymd(new Date());

        var id = Ui.qsInt('id');
        var ext = params.get('ext');
        if (!(ext && /^[a-z]+-[0-9a-f]{16}$/.test(ext))) ext = null;
        if (id || ext) {
            // Trang đọc riêng: không dựng bộ lọc/danh sách. Lấy trang tin đầu để có tên game + tin khác cho bài ETOMS.
            document.querySelector('main').classList.add('news-reading');
            Api.get('/api/news?page=1&pageSize=8').catch(function () { return {}; }).then(function (res) {
                games = res.games || [];
                if (id) openArticle(id, res.items); else openExternal(ext);
            });
            return;
        }

        el('news-games').addEventListener('click', function (e) {
            var b = e.target.closest('[data-game]');
            if (!b) return;
            state.game = b.dataset.game;
            reload();
        });
        el('news-source').addEventListener('change', function () { state.source = this.value; reload(); });
        document.querySelectorAll('[data-range]').forEach(function (b) {
            b.addEventListener('click', function () {
                setRange(b.dataset.range);
                if (b.dataset.range === 'custom') { el('news-from').focus(); if (!state.from && !state.to) return; }
                reload();
            });
        });
        el('news-from').addEventListener('change', onCustomDate);
        el('news-to').addEventListener('change', onCustomDate);
        var timer;
        el('news-search').addEventListener('input', function () {
            clearTimeout(timer);
            timer = setTimeout(function () { state.q = el('news-search').value.trim(); reload(); }, 350);
        });
        el('news-reset').addEventListener('click', function () {
            state.source = 'all'; state.q = ''; el('news-search').value = ''; el('news-from').value = ''; el('news-to').value = '';
            setRange('all'); reload();
        });
        el('news-pager').addEventListener('click', function (e) {
            var b = e.target.closest('[data-page]');
            if (!b || b.disabled) return;
            state.page = parseInt(b.dataset.page, 10);
            load();
            el('news-list-title').scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
        syncUrl();
        load();
    });
})();
