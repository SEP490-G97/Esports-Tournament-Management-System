(function () {
    function renderTicker(events) {
        var root = document.getElementById('ticker-root');
        if (!events || !events.length) { root.innerHTML = ''; return; }
        var innerRepeat = Math.max(1, Math.ceil(8.0 / events.length));
        var items = '';
        // Mỗi mục là một liên kết: "ĐĂNG KÝ" -> trang đăng ký của đúng giải đó, còn lại -> trang giải.
        // Chỉ bản đầu nhận focus bàn phím; các bản lặp (để chạy vòng) ẩn với trình đọc màn hình.
        for (var half = 0; half < 2; half++) {
            for (var rep = 0; rep < innerRepeat; rep++) {
                events.forEach(function (e) {
                    var first = half === 0 && rep === 0;
                    var url = Ui.safeUrl(e.url);
                    var inner = '<b class="ticker-label ticker-' + Ui.escapeHtml((e.kind || '').toLowerCase()) + '">' + Ui.escapeHtml(e.label) + '</b> ' + Ui.escapeHtml(e.text);
                    items += (url
                        ? '<a class="ticker-item" href="' + Ui.escapeHtml(url) + '" data-kind="' + Ui.escapeHtml(e.kind || '') + '"' + (first ? '' : ' tabindex="-1" aria-hidden="true"') + '>' + inner + '</a>'
                        : '<span class="ticker-item"' + (first ? '' : ' aria-hidden="true"') + '>' + inner + '</span>') +
                        '<span class="ticker-dot" aria-hidden="true">◆</span>';
                });
            }
        }
        root.innerHTML =
            '<section class="fullbleed ticker-bar"><div class="wrap ticker-wrap">' +
            '<span class="ticker-tag">SỰ KIỆN</span>' +
            '<div class="ticker-view"><div class="ticker-track">' + items + '</div></div>' +
            '</div></section>';
        // Ban tổ chức / Admin không đăng ký thi đấu: bấm "ĐĂNG KÝ" thì mở trang giải thay vì trang đăng ký.
        root.querySelectorAll('a.ticker-item[data-kind="REGISTER"]').forEach(function (a) {
            a.addEventListener('click', function (ev) {
                var role = document.documentElement.getAttribute('data-role');
                if (role === 'ORGANIZER' || role === 'ADMIN') {
                    var m = /tournamentId=(\d+)/.exec(a.getAttribute('href') || '');
                    if (m) { ev.preventDefault(); location.href = '/bracket/view.html?id=' + m[1]; }
                }
            });
        });
    }

    function setStat(id, value) {
        var el = document.getElementById(id);
        if (!el) return;
        el.classList.remove('stat-loading');
        el.setAttribute('data-count', String(value || 0));
        el.textContent = '0';
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

    // Nút dưới thẻ giải: tuyển thủ thấy "Đăng ký cho đội" khi giải đang mở đăng ký; Ban tổ chức của
    // CHÍNH giải đó thấy "Quản lý đăng ký" (đang mở đăng ký) hoặc "Quản lý giải" (đã đóng/đang diễn ra).
    function featActions(t, isOrganizer) {
        var buttons = '';
        if (t.registrationOpen)
            buttons += '<a class="btn btn-primary btn-sm" data-hide-roles="ORGANIZER ADMIN" href="/registration/register.html?tournamentId=' + Number(t.tournamentId) + '">Đăng ký cho đội</a>';
        if (isOrganizer)
            buttons += t.registrationOpen
                ? '<a class="btn btn-ghost btn-sm" href="/registration/review.html?tournamentId=' + Number(t.tournamentId) + '">Quản lý đăng ký</a>'
                : '<a class="btn btn-ghost btn-sm" href="/tournament/manage.html?id=' + Number(t.tournamentId) + '">Quản lý giải</a>';
        // Luôn có hàng nút (kể cả rỗng) với chiều cao cố định để mọi thẻ trong hàng cao bằng nhau.
        return '<div class="btn-row feat-actions">' + buttons + '</div>';
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
                // Cột flex: thẻ giãn theo chiều cao, hàng nút nằm TRONG cột (trước đây thẻ h-100 + nút ở dưới
                // làm nút tràn đè lên thẻ của hàng sau).
                '<div class="col-12 col-md-6 col-lg-4 d-flex flex-column">' +
                '<a class="feat-card d-block flex-grow-1" href="/bracket/view.html?id=' + t.tournamentId + '" style="color:inherit">' +
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
                featActions(t, organizedIds[t.tournamentId]) +
                '</div>';
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
        // Ô đội dạng thẻ dọc: logo, tên, tựa game, sĩ số theo luật game và số trận thắng.
        var html = '<div class="row g-3">';
        list.forEach(function (t) {
            var logo = Ui.safeUrl(t.logoUrl)
                ? '<img class="ht-logo" src="' + Ui.escapeHtml(Ui.safeUrl(t.logoUrl)) + '" alt="" loading="lazy" />'
                : '<span class="ht-logo ht-logo-text">' + Ui.escapeHtml(t.tag && t.tag.length > 0 ? t.tag.substring(0, 2) : '?') + '</span>';
            html +=
                '<div class="col-6 col-md-4 col-xl-2">' +
                '<a class="home-team h-100" href="/team/details.html?id=' + Number(t.teamId) + '" aria-label="Hồ sơ đội ' + Ui.escapeHtml(t.name) + '">' +
                logo +
                '<div class="ht-name" title="' + Ui.escapeHtml(t.name) + '">' + Ui.escapeHtml(t.name) + '</div>' +
                '<div class="ht-tag">[' + Ui.escapeHtml(t.tag) + ']</div>' +
                '<span class="ht-game">' + Ui.escapeHtml(t.gameTitle) + '</span>' +
                '<div class="ht-stats">' +
                '<div><strong>' + Number(t.memberCount) + '<small>/' + Number(t.maxRoster) + '</small></strong><span>thành viên</span></div>' +
                '<div><strong>' + Number(t.wins) + '</strong><span>trận thắng</span></div>' +
                '</div>' +
                '<div class="ht-captain" title="Đội trưởng">★ ' + Ui.escapeHtml(t.captainName) + '</div>' +
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
            // Bấm vào game -> danh sách giải lọc đúng game đó.
            var n = Number(g.tournamentCount) || 0;
            var body = icon + '<div class="minw-0"><div class="gt">' + Ui.escapeHtml(g.title) + '</div><div class="gc">' + (n ? n + ' giải' : 'Chưa có giải') + '</div></div>';
            html += '<div class="col-6 col-sm-4 col-lg-2"><a class="game-chip game-chip-link h-100" href="/tournament/index.html?gameId=' + Number(g.gameId) + '"' +
                ' aria-label="Xem các giải ' + Ui.escapeHtml(g.title) + '">' + body + '</a></div>';
        });
        root.innerHTML = html;
    }

    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDate(iso) {
        var d = new Date(iso);
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + '/' + d.getFullYear();
    }

    var NEWS_IMAGES = ['/images/news/news-1.jpg', '/images/news/news-2.jpg', '/images/news/news-3.jpg'];

    // UC-46 — kênh đang phát trực tiếp; bấm vào mở đúng kênh đó ở tab "Trực tiếp" của giải.
    function renderLive(list) {
        var band = document.getElementById('live-band');
        var row = document.getElementById('live-row');
        if (!band || !row) return;
        if (!list || !list.length) { band.hidden = true; return; }
        var esc = Ui.escapeHtml;
        row.innerHTML = list.map(function (l) {
            return '<div class="col-12 col-md-6 col-xl-3"><a class="live-card h-100" href="/tournament/live.html?id=' + Number(l.tournamentId) +
                '&stream=' + Number(l.livestreamId) + '">' +
                '<span class="badge badge-live"><span class="live-dot"></span>Đang phát</span>' +
                '<span class="live-card-title">' + esc(l.title) + '</span>' +
                '<span class="live-card-sub">' + esc(l.tournamentName) + ' · ' + esc(l.platformLabel) + '</span>' +
                '<span class="live-card-go">Xem ngay →</span></a></div>';
        }).join('');
        band.hidden = false;
    }

    // UC-42 — nhà tài trợ thật của các giải đang công khai. Ô đầu (hạng cao nhất) to, các ô sau nhỏ.
    // Bấm được: website nhà tài trợ (tab mới) và trang giải được tài trợ.
    function renderSponsors(list) {
        var band = document.getElementById('sponsor-band');
        var row = document.getElementById('sponsor-row');
        if (!band || !row) return;
        if (!list || !list.length) { band.hidden = true; return; }
        var esc = Ui.escapeHtml;
        function logo(s, cls) {
            var src = Ui.safeUrl(s.logoUrl);
            return src
                ? '<img class="' + cls + '" src="' + esc(src) + '" alt="Logo ' + esc(s.name) + '" loading="lazy" />'
                : '<span class="' + cls + ' sponsor-initial">' + esc((s.name || '?').trim().charAt(0).toUpperCase()) + '</span>';
        }
        var main = list[0];
        var mainSite = Ui.safeUrl(main.websiteUrl);
        var html = '<div class="col-12' + (list.length > 1 ? ' col-lg-6' : '') + '"><div class="ad-slot ad-main h-100">' +
            '<div class="min-w-0"><div class="ad-kicker">Nhà tài trợ hạng ' + esc(main.tierLabel) + '</div>' +
            '<div class="ad-title">' + esc(main.name) + '</div>' +
            '<div class="ad-sub">Đồng hành cùng ' + esc(main.tournamentName) + '.</div>' +
            '<div class="btn-row mt-3 mb-0">' +
            (mainSite ? '<a class="btn btn-primary btn-sm" href="' + esc(mainSite) + '" target="_blank" rel="noopener noreferrer sponsored">Website nhà tài trợ ↗</a>' : '') +
            '<a class="btn btn-ghost btn-sm sponsor-ghost" href="/bracket/view.html?id=' + Number(main.tournamentId) + '">Xem giải</a></div></div>' +
            logo(main, 'sponsor-logo-lg') + '</div></div>';
        list.slice(1).forEach(function (s) {
            var site = Ui.safeUrl(s.websiteUrl);
            var href = site || '/bracket/view.html?id=' + Number(s.tournamentId);
            html += '<div class="col-6 col-lg-3"><a class="ad-slot sponsor-card h-100" href="' + esc(href) + '"' +
                (site ? ' target="_blank" rel="noopener noreferrer sponsored"' : '') + '>' +
                logo(s, 'sponsor-logo') +
                '<span class="sponsor-name">' + esc(s.name) + '</span>' +
                '<span class="badge badge-gold">Hạng ' + esc(s.tierLabel) + '</span>' +
                '<span class="sponsor-tour">' + esc(s.tournamentName) + '</span></a></div>';
        });
        row.innerHTML = html;
        band.hidden = false;
    }

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
            renderSponsors(summary.sponsors);
            renderLive(summary.liveNow);
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
