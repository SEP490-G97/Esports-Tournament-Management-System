(function () {
    function renderTicker(events) {
        var root = document.getElementById('ticker-root');
        if (!events || !events.length) { root.innerHTML = ''; return; }
        var innerRepeat = Math.max(1, Math.ceil(8.0 / events.length));
        var items = '';
        for (var half = 0; half < 2; half++) {
            for (var rep = 0; rep < innerRepeat; rep++) {
                events.forEach(function (e) {
                    items += '<span class="ticker-item"><b>' + Ui.escapeHtml(e.label) + '</b> ' + Ui.escapeHtml(e.text) + '</span><span class="ticker-dot">◆</span>';
                });
            }
        }
        root.innerHTML =
            '<section class="fullbleed ticker-bar"><div class="wrap ticker-wrap">' +
            '<span class="ticker-tag">SỰ KIỆN</span>' +
            '<div class="ticker-view"><div class="ticker-track">' + items + '</div></div>' +
            '</div></section>';
    }

    function setStat(id, value) {
        var el = document.getElementById(id);
        if (!el) return;
        el.setAttribute('data-count', String(value || 0));
    }

    function animateCounters() {
        function animateCount(el) {
            var target = parseInt(el.getAttribute('data-count'), 10) || 0;
            if (target === 0) { el.textContent = '0'; return; }
            var start = performance.now(), dur = 1100;
            function tick(now) {
                var p = Math.min((now - start) / dur, 1);
                el.textContent = Math.round(target * (1 - Math.pow(1 - p, 3))).toLocaleString('vi-VN');
                if (p < 1) requestAnimationFrame(tick);
            }
            requestAnimationFrame(tick);
        }
        var counters = document.querySelectorAll('[data-count]');
        if ('IntersectionObserver' in window) {
            var co = new IntersectionObserver(function (es) {
                es.forEach(function (e) { if (e.isIntersecting) { animateCount(e.target); co.unobserve(e.target); } });
            }, { threshold: 0.5 });
            counters.forEach(function (el) { co.observe(el); });
        } else { counters.forEach(animateCount); }
    }

    function wireReveal() {
        var revealTargets = document.querySelectorAll('.home-section, .ad-band .wrap, .subscribe-band .wrap, .feat-card, .team-card, .game-chip, .step');
        revealTargets.forEach(function (el, i) { el.classList.add('reveal'); el.style.transitionDelay = ((i % 6) * 60) + 'ms'; });
        if ('IntersectionObserver' in window) {
            var io = new IntersectionObserver(function (es) {
                es.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); } });
            }, { threshold: 0.12 });
            revealTargets.forEach(function (el) { io.observe(el); });
        } else { revealTargets.forEach(function (el) { el.classList.add('in'); }); }
    }

    function wireHeroCarousel() {
        var hero = document.getElementById('heroCarousel');
        if (!hero) return;
        var slides = hero.querySelectorAll('.hero-slide');
        var dots = hero.querySelectorAll('.hero-dots button');
        var current = 0, timer = null;
        function show(i) {
            current = (i + slides.length) % slides.length;
            slides.forEach(function (s, idx) { s.classList.toggle('active', idx === current); });
            dots.forEach(function (d, idx) { d.classList.toggle('active', idx === current); });
        }
        function start() { timer = setInterval(function () { show(current + 1); }, 6000); }
        function stop() { if (timer) { clearInterval(timer); timer = null; } }
        dots.forEach(function (d) { d.addEventListener('click', function () { show(parseInt(d.dataset.slide, 10)); stop(); start(); }); });
        hero.addEventListener('mouseenter', stop);
        hero.addEventListener('mouseleave', start);
        if (slides.length > 1) start();
    }

    function renderFeatured(list, organizedIds) {
        var root = document.getElementById('featured-list');
        if (!list || !list.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Chưa có giải đấu nào</h3><p>Các giải đã công bố sẽ hiển thị tại đây.</p></div></div>';
            return;
        }
        organizedIds = organizedIds || {};
        var html = '<div class="row g-4">';
        list.forEach(function (t) {
            var pct = t.maxTeams === 0 ? 0 : Math.floor(t.teams * 100 / t.maxTeams);
            html +=
                '<div class="col-12 col-md-6 col-lg-4">' +
                '<a class="feat-card d-block h-100" href="/bracket/view.html?id=' + t.tournamentId + '" style="color:inherit">' +
                '<div class="feat-cover" style="background-image:linear-gradient(180deg, rgba(10,14,23,.2), rgba(10,14,23,.8)), url(\'' + Ui.escapeHtml(t.imageUrl) + '\')">' +
                '<span class="badge ' + Ui.escapeHtml(t.statusClass) + '">' + Ui.escapeHtml(t.status) + '</span>' +
                '<span class="feat-game">' + Ui.escapeHtml(t.game) + '</span></div>' +
                '<div class="feat-body">' +
                '<div class="feat-name">' + Ui.escapeHtml(t.name) + '</div>' +
                '<div class="feat-meta">' +
                '<div><span class="k">Giải thưởng</span><span class="v accent">' + Ui.escapeHtml(t.prize) + '</span></div>' +
                '<div><span class="k">Số đội</span><span class="v">' + t.teams + '/' + t.maxTeams + '</span></div>' +
                '</div>' +
                '<div class="feat-bar"><span style="width:' + pct + '%"></span></div>' +
                '</div></a>' +
                '<div class="btn-row mt-2">' +
                (t.registrationOpen ? '<a class="btn btn-primary" data-hide-roles="ORGANIZER ADMIN" href="/registration/register.html?tournamentId=' + Number(t.tournamentId) + '">Đăng ký cho đội</a>' : '') +
                // Chỉ hiện cho đúng Ban tổ chức của giải này (khớp quyền thật ở API quản lý đăng ký) —
                // trước đây hiện cho mọi người, bấm vào thì mới bị chặn, gây hiểu lầm là lỗi.
                (organizedIds[t.tournamentId] ? '<a class="btn btn-ghost" href="/registration/review.html?tournamentId=' + Number(t.tournamentId) + '">Quản lý đăng ký</a>' : '') +
                '</div></div>';
        });
        html += '</div>';
        root.innerHTML = html;
    }

    function renderTeams(list) {
        var root = document.getElementById('teams-list');
        if (!list || !list.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Chưa có đội nào</h3><p>Hãy là đội đầu tiên trên hệ thống.</p><a class="btn btn-primary" data-hide-roles="ORGANIZER ADMIN" href="/team/create.html">Tạo đội mới</a></div></div>';
            return;
        }
        var html = '<div class="row g-3">';
        list.forEach(function (t) {
            var logo = t.logoUrl
                ? '<img class="team-logo" src="' + Ui.escapeHtml(t.logoUrl) + '" alt="' + Ui.escapeHtml(t.name) + '" />'
                : '<span class="team-logo">' + Ui.escapeHtml(t.tag && t.tag.length > 0 ? t.tag.substring(0, 1) : '?') + '</span>';
            html +=
                '<div class="col-12 col-md-6 col-lg-4">' +
                '<a class="team-card d-block h-100" href="/team/details.html?id=' + t.teamId + '">' +
                '<div class="top">' + logo +
                '<div><div class="name">' + Ui.escapeHtml(t.name) + '</div><div class="tag">[' + Ui.escapeHtml(t.tag) + '] · ' + Ui.escapeHtml(t.gameTitle) + '</div></div>' +
                '</div>' +
                '<div class="meta"><span>' + Ui.escapeHtml(t.captainName) + '</span><span class="badge badge-soft">' + t.memberCount + ' thành viên</span></div>' +
                '</a></div>';
        });
        html += '</div>';
        root.innerHTML = html;
    }

    function renderGames(list) {
        var root = document.getElementById('games-list');
        var html = '';
        (list || []).forEach(function (g) {
            var icon = g.logoUrl
                ? '<img src="' + Ui.escapeHtml(g.logoUrl) + '" alt="' + Ui.escapeHtml(g.title) + '" />'
                : '<span class="game-ico">' + Ui.escapeHtml(g.shortCode && g.shortCode.length > 0 ? g.shortCode.substring(0, 1) : '?') + '</span>';
            html +=
                '<div class="col-6 col-sm-4 col-lg-2"><div class="game-chip h-100">' + icon +
                '<div><div class="gt">' + Ui.escapeHtml(g.title) + '</div><div class="gc">' + Ui.escapeHtml(g.shortCode) + '</div></div>' +
                '</div></div>';
        });
        root.innerHTML = html;
    }

    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDate(iso) {
        var d = new Date(iso);
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + '/' + d.getFullYear();
    }

    var NEWS_IMAGES = ['/images/news/news-1.jpg', '/images/news/news-2.jpg', '/images/news/news-3.jpg'];

    function renderNews(list) {
        var root = document.getElementById('news-root');
        if (!list || !list.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Chưa có tin tức nào</h3></div></div>';
            return;
        }
        var lead = list[0];
        var rest = list.slice(1);

        var leadHtml =
            '<div class="news-lead"><div class="news-cover" style="background-image:linear-gradient(180deg, rgba(10,14,23,.15), rgba(10,14,23,.75)), url(\'' + NEWS_IMAGES[0] + '\')"><span class="news-cat">Tin tức</span></div>' +
            '<div class="news-lead-body">' +
            (lead.publishedAt ? '<div class="news-meta">' + fmtDate(lead.publishedAt) + '</div>' : '') +
            '<h3>' + Ui.escapeHtml(lead.title) + '</h3>' +
            '<p>' + Ui.escapeHtml(lead.excerpt) + '</p>' +
            '</div></div>';

        var listHtml = '<div class="news-list">';
        rest.forEach(function (n, i) {
            var img = NEWS_IMAGES[(i + 1) % NEWS_IMAGES.length];
            listHtml +=
                '<div class="news-item"><span class="news-thumb" style="background-image:url(\'' + img + '\');background-size:cover;background-position:center;color:transparent"></span>' +
                '<div><div class="news-cat-sm">Tin tức</div><h4>' + Ui.escapeHtml(n.title) + '</h4></div></div>';
        });
        listHtml += '</div>';

        root.innerHTML = '<div class="news-grid">' + leadHtml + listHtml + '</div>';
    }

    function wireSubscribeForm() {
        var form = document.getElementById('subscribe-form');
        var flash = document.getElementById('subscribe-flash');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);
            var email = form.querySelector('[name="Email"]').value;
            try {
                var res = await Api.post('/api/home/subscribe', { Email: email });
                Ui.showAlert(flash, 'success', res.message);
                form.reset();
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                if (!err.fieldErrors || !err.fieldErrors.Email) {
                    Ui.showAlert(flash, 'danger', err.message || 'Vui lòng kiểm tra lại email.');
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        wireHeroCarousel();
        wireSubscribeForm();
        try {
            var summary = await Api.get('/api/home/summary');
            renderTicker(summary.events);
            setStat('stat-tournaments', summary.stats.tournaments);
            setStat('stat-teams', summary.stats.teams);
            setStat('stat-players', summary.stats.players);
            setStat('stat-games', summary.stats.games);

            // "Quản lý đăng ký" chỉ nên hiện cho đúng Ban tổ chức của giải đó — hỏi riêng
            // (không gộp vào /api/home/summary, vì API đó dùng chung 1 cache 30s cho mọi người xem).
            var organizedIds = {};
            try {
                var mine = await Api.get('/api/marketing/mine/tournaments');
                (mine.data || mine || []).forEach(function (t) { organizedIds[t.tournamentId] = true; });
            } catch (e) { /* chưa đăng nhập hoặc không phải Ban tổ chức — không hiện nút nào cả */ }

            renderFeatured(summary.featured, organizedIds);
            renderTeams(summary.teams);
            renderGames(summary.games);
            renderNews(summary.news);
        } catch (e) {
            renderFeatured([]);
            renderTeams([]);
            renderGames([]);
            renderNews([]);
        }
        wireReveal();
        animateCounters();
    });
})();
