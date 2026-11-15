// Carousel Banner tổng trên trang chủ (nguồn: GET /api/banners). Không có banner nào → ẩn hẳn.
(function () {
    'use strict';
    var ROTATE_MS = 6000;

    function safe(u) { return window.Ui ? Ui.safeUrl(u) : u; }

    async function init() {
        var root = document.getElementById('sysBanner');
        if (!root) return;
        var items;
        try {
            var res = await fetch('/api/banners');
            if (!res.ok) return;
            items = await res.json();
        } catch (e) { return; }
        if (!Array.isArray(items) || items.length === 0) return;

        var slides = [], dots = [], cur = 0, timer = null;
        items.forEach(function (b, i) {
            var href = safe(b.linkUrl), imgSrc = safe(b.imageUrl);
            if (!imgSrc) return;
            var s = document.createElement(href ? 'a' : 'div');
            s.className = 'etb-slide' + (i === 0 ? ' active' : '');
            if (href) s.href = href;
            var img = document.createElement('img');
            img.src = imgSrc; img.alt = b.title || ''; img.loading = i === 0 ? 'eager' : 'lazy';
            var cap = document.createElement('div'); cap.className = 'etb-cap';
            var tag = document.createElement('span');
            tag.className = 'etb-tag' + (b.bannerType === 'GIVEAWAY' ? ' gift' : '');
            tag.textContent = b.bannerType === 'GIVEAWAY' ? 'GIVEAWAY' : 'SỰ KIỆN';
            var t = document.createElement('b'); t.textContent = b.title;
            cap.appendChild(tag); cap.appendChild(t);
            if (b.description) { var d = document.createElement('span'); d.className = 'd'; d.textContent = b.description; cap.appendChild(d); }
            s.appendChild(img); s.appendChild(cap);
            root.appendChild(s); slides.push(s);
        });
        if (slides.length === 0) return;
        root.hidden = false;

        function show(i) {
            slides[cur].classList.remove('active'); if (dots[cur]) dots[cur].classList.remove('active');
            cur = (i + slides.length) % slides.length;
            slides[cur].classList.add('active'); if (dots[cur]) dots[cur].classList.add('active');
        }
        function start() {
            stop();
            if (slides.length > 1 && !window.matchMedia('(prefers-reduced-motion: reduce)').matches)
                timer = setInterval(function () { show(cur + 1); }, ROTATE_MS);
        }
        function stop() { if (timer) { clearInterval(timer); timer = null; } }

        if (slides.length > 1) {
            var prev = document.createElement('button'); prev.type = 'button'; prev.className = 'etb-nav prev'; prev.setAttribute('aria-label', 'Banner trước'); prev.textContent = '‹';
            var next = document.createElement('button'); next.type = 'button'; next.className = 'etb-nav next'; next.setAttribute('aria-label', 'Banner sau'); next.textContent = '›';
            prev.addEventListener('click', function () { show(cur - 1); start(); });
            next.addEventListener('click', function () { show(cur + 1); start(); });
            var dw = document.createElement('div'); dw.className = 'etb-dots';
            slides.forEach(function (_, i) {
                var d = document.createElement('button'); d.type = 'button'; d.setAttribute('aria-label', 'Banner ' + (i + 1));
                if (i === 0) d.className = 'active';
                d.addEventListener('click', function () { show(i); start(); });
                dw.appendChild(d); dots.push(d);
            });
            root.appendChild(prev); root.appendChild(next); root.appendChild(dw);
            root.addEventListener('mouseenter', stop);
            root.addEventListener('mouseleave', start);
            document.addEventListener('visibilitychange', function () { document.hidden ? stop() : start(); });
            start();
        }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();