// "Layout" dùng chung cho mọi trang tĩnh: chèn nav.html/confirm-modal.html, bật theme-toggle,
// Mọi trang phải có sẵn <div id="site-header"></div> và <div id="confirm-modal-root"></div>,
// và nạp theo thứ tự: auth.js, api.js, ui.js, layout.js (theme-init.js nạp riêng trong <head>).
(function () {
    // ===== Quản trị viên chỉ dùng khu quản trị =====
    // Admin không có hồ sơ tuyển thủ / đội / giải: đăng nhập là vào thẳng Bảng quản trị, đầu trang chỉ còn mục
    // quản trị. Mở trang khác (trang chủ, hồ sơ...) thì đưa về Bảng quản trị. Trang đơn vị tổ chức vẫn mở được
    // vì Admin duyệt / đình chỉ đơn vị ở đó. Đây chỉ là điều hướng giao diện; quyền thật vẫn chặn ở API.
    var ADMIN_HOME = '/admin/index.html';
    function adminPathAllowed(path) {
        path = (path || '').toLowerCase();
        return ['/admin/', '/organization/', '/auth/', '/errors/', '/events/', '/news/'] /* admin bấm "Xem" tin ở trang quản trị tin tức */.some(function (p) { return path.indexOf(p) === 0; });
    }
    function cachedRole() {
        // Chỉ tin vai trò đã lưu khi còn token đăng nhập (đăng xuất / hết phiên thì coi như khách).
        try { var u = window.Auth && Auth.getAccessToken() && Auth.getUser(); return u && u.role; } catch (e) { return null; }
    }
    // Chuyển ngay khi nạp script (dùng vai trò đã lưu lúc đăng nhập) để không thấy trang chủ nháy lên rồi mới chuyển.
    if (cachedRole() === 'ADMIN' && !adminPathAllowed(location.pathname)) { location.replace(ADMIN_HOME); return; }

    function applyAdminNav() {
        var nav = document.querySelector('#etomsNavCollapse nav');
        if (!nav || nav.dataset.admin === '1') return;
        nav.dataset.admin = '1';
        nav.innerHTML =
            // MỘT menu quản trị cho mọi trang admin (thay thanh tab riêng admin-nav của từng trang).
            '<a href="/admin/index.html" data-nav="admin/index">Tổng quan</a>' +
            '<a href="/admin/stats.html" data-nav="admin/stats">Thống kê</a>' +
            '<a href="/admin/user.html" data-nav="admin/user">Tài khoản</a>' +
            '<a href="/admin/news.html" data-nav="admin/news">Tin tức</a>' +
            '<a href="/admin/ads.html" data-nav="admin/ads">Quảng cáo</a>' +
            '<a href="/admin/games.html" data-nav="admin/games">Tựa game</a>' +
            '<a href="/admin/game-rules.html" data-nav="admin/game-rules">Luật game</a>' +
            '<a href="/organization/index.html" data-nav="organization">Đơn vị</a>' +
            '<a href="/admin/audit-log.html" data-nav="admin/audit-log">Nhật ký</a>';
        var header = document.querySelector('.etoms-nav');
        if (header) header.classList.add('nav-many');
        var brand = document.querySelector('.etoms-nav .brand');
        if (brand) brand.setAttribute('href', ADMIN_HOME);
        applyActiveNav();
    }

    // Mọi ô số điện thoại (type="tel"): chỉ nhận chữ số, tối đa 10 số, bàn phím số trên điện thoại (góp ý nhóm mục 25).
    // Máy chủ kiểm lại: đúng 10 số, bắt đầu bằng 0 (Utils/PhoneNumber.cs).
    function wirePhoneInputs() {
        document.querySelectorAll('input[type="tel"]').forEach(function (i) {
            i.maxLength = 10; i.inputMode = 'numeric'; i.pattern = '0[0-9]{9}';
            if (!i.placeholder) i.placeholder = 'Ví dụ: 0912345678';
        });
        document.addEventListener('input', function (e) {
            var i = e.target;
            if (!i || i.type !== 'tel') return;
            var digits = i.value.replace(/\D/g, '').slice(0, 10);
            if (digits !== i.value) i.value = digits;
        });
    }

    function applyActiveNav() {
        var path = location.pathname.toLowerCase();
        document.querySelectorAll('#etomsNavCollapse nav a[data-nav]').forEach(function (a) {
            var key = a.getAttribute('data-nav');
            var match = (key === 'home' && path === '/' ) ||
                (key !== 'home' && path.indexOf('/' + key) === 0);
            a.classList.toggle('active', match);
        });
    }

    function wireThemeToggle() {
        var btn = document.getElementById('theme-toggle');
        if (!btn) return;
        btn.addEventListener('click', function () {
            var isLight = document.documentElement.getAttribute('data-theme') === 'light';
            if (isLight) {
                document.documentElement.removeAttribute('data-theme');
                try { localStorage.setItem('etoms-theme', 'dark'); } catch (e) {}
            } else {
                document.documentElement.setAttribute('data-theme', 'light');
                try { localStorage.setItem('etoms-theme', 'light'); } catch (e) {}
            }
        });
    }

    // Người dùng hiện tại (JWT, auth.js) — dùng chung kết quả với trang qua Ui.currentUser().
    async function resolveCurrentUser() {
        var me = await Ui.currentUser();
        return me || { isAuthenticated: false };
    }

    async function wireAuthWidget() {
        var slot = document.getElementById('nav-auth-slot');
        if (!slot) return;
        var loggedOutHtml = '<a class="btn btn-primary nav-auth-login" href="/auth/login.html">Đăng nhập</a>';
        try {
            var me = await resolveCurrentUser();
            if (me.isAuthenticated && me.role === 'ADMIN') {
                // Vai trò lưu trong máy có thể cũ (vd vừa đổi tài khoản) — kiểm lại theo /me.
                if (!adminPathAllowed(location.pathname)) { location.replace(ADMIN_HOME); return me; }
                applyAdminNav();
            }
            if (me.isAuthenticated) {
                renderUserDropdown(slot, me);
                wireNotificationBell(slot);
                await loadMyTeam(me);
                loadMyOrgRoles(me);
            } else {
                slot.innerHTML = loggedOutHtml;
            }
            applyRoleVisibility(me);
            return me;
        } catch (e) {
            slot.innerHTML = loggedOutHtml;
            applyRoleVisibility({ isAuthenticated: false });
            return { isAuthenticated: false };
        }
    }

    // Khung "Xin chào, X" giờ là 1 dropdown: bấm avatar/tên để mở, bên trong có "Đội của tôi"
    // (nối thẳng tới team/details.html?id=, hoặc báo "Không có đội" nếu chưa ở đội nào) + Đăng xuất.
    function renderUserDropdown(slot, me) {
        slot.innerHTML =
            '<div class="nav-user-dd" id="nav-user-dd">' +
            '  <button type="button" class="nav-user-trigger" id="nav-user-trigger" aria-expanded="false">' +
            '    <span class="av">' + Ui.escapeHtml((me.displayName || '?').substring(0, 1).toUpperCase()) + '</span>' +
            '    <span><span class="nav-hello">Xin chào, </span><span class="who">' + Ui.escapeHtml(me.displayName || '') + '</span></span>' +
            '    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" class="nav-user-caret"><path d="M6 9l6 6 6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>' +
            '  </button>' +
            '  <div class="nav-user-menu" id="nav-user-menu">' +
            (me.role ? '    <span class="nav-user-role">' + Ui.escapeHtml(roleLabel(me.role)) + '</span>' : '') +
            (me.role === 'ORGANIZER' ? '    <div class="nav-user-orgs" id="nav-user-orgs"></div>' : '') +
            // Mục RIÊNG từng vai trò (PermissionMatrix) — đầu trang chỉ giữ mục chung. Mỗi việc một lối vào:
            // "Tạo giải mới" nằm ở trang Giải đấu của tôi; "Đơn của đội" ở Đơn đăng ký của tôi.
            (me.role === 'ADMIN'
                // Admin không có hồ sơ cá nhân: chỉ giữ đổi mật khẩu.
                ? '    <a class="nav-user-menu-item" href="/auth/change-password.html">Đổi mật khẩu</a>'
                : '    <a class="nav-user-menu-item" href="/profile/index.html">Hồ sơ cá nhân</a>') +
            (me.role === 'PLAYER'
                ? '    <a class="nav-user-menu-item nav-user-myteam" id="nav-user-myteam" href="/team/mine.html">Các đội của tôi</a>' +
                  '    <a class="nav-user-menu-item" href="/registration/mine.html">Đơn đăng ký của tôi</a>' +
                  '    <a class="nav-user-menu-item" href="/free-agent/index.html">Tìm đội / tìm thành viên</a>' : '') +
            (me.role === 'ORGANIZER'
                ? '    <a class="nav-user-menu-item" href="/registration/organizer.html">Giải đấu của tôi</a>' +
                  '    <a class="nav-user-menu-item" href="/referee/index.html">Trận tôi làm trọng tài</a>' +
                  '    <a class="nav-user-menu-item" href="/result/disputes.html">Tranh chấp</a>' +
                  '    <a class="nav-user-menu-item" href="/organization/index.html?mine=1">Đơn vị tổ chức của tôi</a>' +
                  '    <a class="nav-user-menu-item" href="/marketing/tools.html">Công cụ truyền thông</a>' : '') +

            '    <div class="nav-user-menu-sep"></div>' +
            '    <a class="nav-user-menu-item" href="#" id="nav-logout">Đăng xuất</a>' +
            '  </div>' +
            '</div>';

        var trigger = document.getElementById('nav-user-trigger');
        var menu = document.getElementById('nav-user-menu');
        trigger.addEventListener('click', function (e) {
            e.stopPropagation();
            var open = menu.classList.toggle('show');
            trigger.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
        document.addEventListener('click', function (e) {
            if (!menu.classList.contains('show')) return;
            if (!menu.contains(e.target) && e.target !== trigger) {
                menu.classList.remove('show');
                trigger.setAttribute('aria-expanded', 'false');
            }
        });
        document.getElementById('nav-logout').addEventListener('click', function (e) {
            e.preventDefault();
            Auth.logout('/');                                  // thu hồi refresh token, xóa cookie, về trang chủ
        });
    }

    // Chuông thông báo (bảng Notification, API /api/notifications). Hỏi lại máy chủ mỗi 60 giây (polling — chưa có Hub).
    // Bấm một thông báo: đánh dấu đã đọc rồi mở liên kết của nó (nếu có).
    function wireNotificationBell(slot) {
        var wrap = document.createElement('div');
        wrap.className = 'nav-bell';
        wrap.innerHTML =
            '<button type="button" class="nav-bell-btn" id="nav-bell-btn" aria-expanded="false" aria-label="Thông báo" title="Thông báo">' +
            '  <svg width="18" height="18" viewBox="0 0 24 24" fill="none"><path d="M6 9a6 6 0 1 1 12 0c0 4.5 1.8 6.3 2.5 7H3.5C4.2 15.3 6 13.5 6 9Z" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/><path d="M10 19.5a2.2 2.2 0 0 0 4 0" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>' +
            '  <span class="nav-bell-count" id="nav-bell-count" hidden>0</span>' +
            '</button>' +
            '<div class="nav-bell-menu" id="nav-bell-menu" role="dialog" aria-label="Thông báo">' +
            '  <div class="nav-bell-head"><b>Thông báo</b><button type="button" class="nav-bell-readall" id="nav-bell-readall">Đánh dấu đã đọc hết</button></div>' +
            '  <div class="nav-bell-list" id="nav-bell-list"><div class="nav-bell-empty">Đang tải…</div></div>' +
            '  <button type="button" class="nav-bell-more" id="nav-bell-more" hidden>Xem thêm</button>' +
            '</div>';
        slot.insertBefore(wrap, slot.firstChild);

        var btn = document.getElementById('nav-bell-btn'), menu = document.getElementById('nav-bell-menu');
        var list = document.getElementById('nav-bell-list'), more = document.getElementById('nav-bell-more');
        var countEl = document.getElementById('nav-bell-count');
        var page = 1, items = [];

        function setCount(n) {
            countEl.hidden = !n;
            countEl.textContent = n > 99 ? '99+' : String(n);
            btn.setAttribute('aria-label', n ? 'Thông báo, ' + n + ' chưa đọc' : 'Thông báo');
        }
        function when(iso) {
            var d = new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z');
            if (isNaN(d.getTime())) return '';
            var mins = Math.round((Date.now() - d.getTime()) / 60000);
            if (mins < 1) return 'Vừa xong';
            if (mins < 60) return mins + ' phút trước';
            if (mins < 1440) return Math.floor(mins / 60) + ' giờ trước';
            return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
        }
        function render() {
            if (!items.length) { list.innerHTML = '<div class="nav-bell-empty">Chưa có thông báo nào.</div>'; return; }
            list.innerHTML = items.map(function (n) {
                return '<button type="button" class="nav-bell-item' + (n.isRead ? '' : ' unread') + '" data-id="' + n.notificationId + '">' +
                    '<span class="t">' + Ui.escapeHtml(n.title || '') + '</span>' +
                    (n.body ? '<span class="b">' + Ui.escapeHtml(n.body) + '</span>' : '') +
                    '<span class="w">' + when(n.sentAt) + '</span></button>';
            }).join('');
        }
        async function load(reset) {
            if (reset) { page = 1; items = []; }
            try {
                var res = await Api.get('/api/notifications?page=' + page + '&pageSize=10');
                items = items.concat(res.items || []);
                setCount(res.unread || 0);
                more.hidden = items.length >= (res.total || 0);
                render();
            } catch (e) { list.innerHTML = '<div class="nav-bell-empty">Không tải được thông báo.</div>'; }
        }
        async function poll() {
            if (document.hidden) return;
            try { setCount((await Api.get('/api/notifications/unread-count')).unread || 0); } catch (e) { }
        }
        function close() { menu.classList.remove('show'); btn.setAttribute('aria-expanded', 'false'); }

        btn.addEventListener('click', function (e) {
            e.stopPropagation();
            var open = menu.classList.toggle('show');
            btn.setAttribute('aria-expanded', open ? 'true' : 'false');
            if (open) load(true);
        });
        document.addEventListener('click', function (e) { if (menu.classList.contains('show') && !wrap.contains(e.target)) close(); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && menu.classList.contains('show')) { close(); btn.focus(); } });
        more.addEventListener('click', function (e) { e.stopPropagation(); page++; load(false); });
        document.getElementById('nav-bell-readall').addEventListener('click', async function (e) {
            e.stopPropagation();
            try { await Api.post('/api/notifications/read-all', {}); } catch (err) { return; }
            items.forEach(function (n) { n.isRead = true; });
            setCount(0);
            render();
        });
        list.addEventListener('click', async function (e) {
            var el = e.target.closest('.nav-bell-item');
            if (!el) return;
            var n = items.find(function (x) { return x.notificationId === Number(el.dataset.id); });
            if (!n) return;
            if (!n.isRead) {
                try { await Api.post('/api/notifications/' + n.notificationId + '/read', {}); n.isRead = true; } catch (err) { }
                setCount(Math.max(0, items.filter(function (x) { return !x.isRead; }).length));
                poll();
                render();
            }
            // Chỉ mở liên kết nội bộ (bắt đầu bằng một dấu /).
            if (n.linkUrl && /^\/(?!\/)/.test(n.linkUrl)) location.href = n.linkUrl;
        });

        poll();
        setInterval(poll, 60 * 1000);
    }

    // Nhớ tạm trong tab (sessionStorage) những câu hỏi lặp lại MỖI lần chuyển trang — database ở xa nên mỗi câu
    // tốn ~0,7 s. Khóa gắn với tài khoản; hết hạn thì hỏi lại máy chủ. Trình duyệt chặn bộ nhớ thì bỏ qua (vẫn chạy đúng).
    function cacheGet(key) {
        try {
            var raw = sessionStorage.getItem('etoms-cache:' + key);
            if (!raw) return null;
            var v = JSON.parse(raw);
            return v && v.exp > Date.now() ? v.val : null;
        } catch (e) { return null; }
    }
    function cacheSet(key, val, ttlMs) {
        try { sessionStorage.setItem('etoms-cache:' + key, JSON.stringify({ val: val, exp: Date.now() + ttlMs })); } catch (e) { }
    }

    async function loadMyTeam(me) {
        var box = document.getElementById('nav-user-myteam');
        if (!box) return;
        // Một tài khoản có thể ở nhiều đội (mỗi tựa game một đội) nên chỉ hiện SỐ đội;
        // bấm vào mở trang /team/mine.html liệt kê đủ. Lỗi thì giữ nguyên link, không có số.
        var key = 'my-team-count:' + (me && me.userId);
        var count = cacheGet(key);
        if (count === null) {
            try {
                var teams = await Api.get('/api/teams/my-teams');
                count = Array.isArray(teams) ? teams.length : 0;
                cacheSet(key, count, 60 * 1000);
            } catch (e) { return; /* giữ link không có số */ }
        }
        box.innerHTML = 'Các đội của tôi <span class="badge badge-soft">' + count + '</span>';
    }

    // Góp ý 22/11 "chưa có chỗ nào hiển thị rõ đang ở role nào": Ban tổ chức thấy vai trò của mình trong từng đơn vị
    // (Chủ nhiệm / Điều hành giải / Truyền thông / Trọng tài) ngay trong menu tài khoản. Nhớ 60 giây trong tab.
    async function loadMyOrgRoles(me) {
        var box = document.getElementById('nav-user-orgs');
        if (!box) return;
        var key = 'my-org-roles:' + (me && me.userId);
        var list = cacheGet(key);
        if (list === null) {
            try {
                var res = await Api.get('/api/organizations/mine');
                list = (res.data || []).filter(function (o) { return o.memberStatus === 'ACTIVE'; })
                    .map(function (o) { return { id: o.organizationId, name: o.name, role: o.roleLabel }; });
                cacheSet(key, list, 60 * 1000);
            } catch (e) { return; }
        }
        box.innerHTML = list.length
            ? list.map(function (o) {
                return '<a class="nav-user-org" href="/organization/details.html?id=' + Number(o.id) + '" title="Vai trò của bạn trong đơn vị này">' +
                    '<b>' + Ui.escapeHtml(o.role) + '</b><span>' + Ui.escapeHtml(o.name) + '</span></a>';
            }).join('')
            : '<span class="nav-user-org muted">Chưa thuộc đơn vị nào — chưa tạo được giải</span>';
    }

    function roleLabel(role) {
        return { ADMIN: 'Quản trị viên', ORGANIZER: 'Ban tổ chức', PLAYER: 'Tuyển thủ' }[role] || role;
    }

    // Hiện mục menu theo vai trò (data-roles trong nav.html): "auth" = đã đăng nhập, còn lại là danh
    // sách Role (vd "ORGANIZER" cho hàng đợi tranh chấp RES-06 — PermissionMatrix: Admin không có). Mặc định các mục này ẩn bằng CSS
    // để không hiện rồi mất khi /me chạy xong. Chỉ là gợi ý hiển thị: quyền thật vẫn chặn ở API/trang đích.
    function applyRoleVisibility(me) {
        document.documentElement.setAttribute('data-role', me.isAuthenticated ? (me.role || 'PLAYER') : 'GUEST');
        var extra = 0;
        document.querySelectorAll('#etomsNavCollapse nav a[data-roles]').forEach(function (a) {
            var roles = a.getAttribute('data-roles').split(',');
            var ok = !!me.isAuthenticated && (roles.indexOf('auth') >= 0 || roles.indexOf(me.role) >= 0);
            a.classList.toggle('role-visible', ok);
            if (ok) extra++;
        });
        // Ban tổ chức/Admin có thêm mục menu: nới khung header để menu nằm trên một dòng.
        var header = document.querySelector('.etoms-nav');
        var adminNav = document.querySelector('#etomsNavCollapse nav[data-admin="1"]');
        if (header) header.classList.toggle('nav-many', extra >= 2 || !!adminNav);
    }

    // Footer dùng chung cho mọi trang chưa có footer riêng (trang chủ giữ footer của nó).
    async function injectFooter() {
        if (document.querySelector('.site-footer')) return;
        if (cachedRole() === 'ADMIN') return;   // chân trang toàn liên kết trang người chơi — Admin không dùng
        var holder = document.createElement('div');
        holder.id = 'site-footer';
        var main = document.querySelector('main');
        if (main && main.parentNode) main.parentNode.insertBefore(holder, main.nextSibling);
        else document.body.appendChild(holder);
        await injectPartial('/partials/footer.html', 'site-footer');
    }

    // Thanh tab trong một giải đấu (giống các nền tảng esports): tab công khai cho mọi người,
    // tab quản lý chỉ hiện khi tài khoản là ban tổ chức của giải (API publish-status trả 200).
    var TOUR_PUBLIC = [
        ['/bracket/view.html', 'Tổng quan'],
        ['/tournament/participants.html', 'Đội tham dự'],
        ['/tournament/bracket.html', 'Nhánh đấu'],
        ['/tournament/standings.html', 'Bảng xếp hạng'],
        ['/tournament/rulebook-history.html', 'Thể lệ'],
        ['/tournament/live.html', 'Trực tiếp']
    ];
    // Menu của Ban tổ chức chia nhóm (hiện thành cột bên trái trên máy tính).
    var TOUR_ORGANIZER_GROUPS = [
        ['Điều hành giải', [
            ['/tournament/manage.html', 'Trạng thái & xuất bản'],
            ['/tournament/dashboard.html', 'Bảng điều hành'],
            ['/registration/review.html', 'Đơn đăng ký', 'tournamentId'],
            ['/bracket/manage.html', 'Chia nhánh'],
            ['/tournament/schedule.html', 'Lịch thi đấu'],
            ['/tournament/referees.html', 'Trọng tài'],
            ['/tournament/livestreams.html', 'Livestream'],
            ['/tournament/stage-transition.html', 'Chuyển vòng'],
            ['/tournament/news.html', 'Tin tức giải'],
            ['/tournament/discipline.html', 'Kỷ luật'],
            ['/tournament/audit-log.html', 'Nhật ký giải']
        ]],
        ['Cấu hình giải', [
            ['/tournament/edit.html', 'Sửa thông tin & thể lệ'],
            ['/tournament/sponsors.html', 'Nhà tài trợ'],
            ['/tournament/clone.html', 'Nhân bản'],
            ['/tournament/cancel.html', 'Hủy giải']
        ]]
    ];

    function tournamentIdOfPage() {
        var path = location.pathname.toLowerCase();
        var q = new URLSearchParams(location.search);
        if (path === '/registration/review.html') return q.get('tournamentId');
        if ((path.indexOf('/tournament/') === 0 && path !== '/tournament/index.html') || path === '/bracket/manage.html' || path === '/bracket/view.html' || path === '/bracket/tiebreaker.html') return q.get('id');
        return null;
    }

    async function wireTournamentNav(me) {
        var id = parseInt(tournamentIdOfPage(), 10);
        var main = document.querySelector('main');
        if (!id || !main) return;
        var isOrganizer = false;
        if (me && me.isAuthenticated && (me.role === 'ORGANIZER' || me.role === 'ADMIN')) {
            // Nhớ 10 phút trong tab: menu trái của BTC hiện ngay khi chuyển giữa các trang của cùng giải,
            // không phải chờ ~0,7 s hỏi lại máy chủ (máy chủ vẫn kiểm quyền ở từng thao tác).
            var orgKey = 'is-organizer:' + me.userId + ':' + id;
            var cached = cacheGet(orgKey);
            if (cached !== null) isOrganizer = cached;
            else {
                try { await Api.get('/api/tournaments/' + id + '/publish-status'); isOrganizer = true; } catch (e) { isOrganizer = false; }
                cacheSet(orgKey, isOrganizer, 10 * 60 * 1000);
            }
        }
        var path = location.pathname.toLowerCase();
        function link(item) {
            var href = item[0] + '?' + (item[2] || 'id') + '=' + id;
            return '<a href="' + href + '"' + (path === item[0] ? ' class="active" aria-current="page"' : '') + '>' + Ui.escapeHtml(item[1]) + '</a>';
        }

        // Phần đầu trang (link quay lại + tiêu đề) giữ nguyên ở trên; menu chèn ngay sau.
        var head = main.querySelector(':scope > .page-head') ||
            Array.prototype.find.call(main.children, function (el) { return el.querySelector && el.querySelector('.page-head'); });

        // MỌI người xem cùng một kiểu: cột menu bên trái + nội dung bên phải (màn hẹp thì menu thành thanh cuộn ngang
        // ở trên). Khách/tuyển thủ chỉ có nhóm "Xem công khai"; Ban tổ chức của giải có thêm nhóm điều hành + cấu hình.
        var aside = document.createElement('nav');
        aside.className = 'tour-sidenav';
        aside.setAttribute('aria-label', 'Điều hướng trong giải');
        aside.innerHTML = '<div class="grp"><div class="grp-title">Xem công khai</div>' + TOUR_PUBLIC.map(link).join('') + '</div>' +
            (isOrganizer ? TOUR_ORGANIZER_GROUPS.map(function (g) {
                return '<div class="grp"><div class="grp-title">' + Ui.escapeHtml(g[0]) + '</div>' + g[1].map(link).join('') + '</div>';
            }).join('') : '');
        var content = document.createElement('div');
        content.className = 'tour-content';
        var shell = document.createElement('div');
        shell.className = 'tour-shell';
        shell.appendChild(aside);
        shell.appendChild(content);

        // Chuyển mọi phần tử SAU tiêu đề vào cột nội dung (giữ nguyên phần tử, id, sự kiện đã gắn).
        var start = head ? head.nextSibling : main.firstChild;
        while (start && start.classList && start.classList.contains('back-link')) start = start.nextSibling;
        var moving = [];
        for (var node = start; node; node = node.nextSibling) moving.push(node);
        moving.forEach(function (n) { content.appendChild(n); });
        main.appendChild(shell);

        var active = aside.querySelector('a.active');
        if (active && active.scrollIntoView && window.matchMedia('(max-width: 991.98px)').matches)
            active.scrollIntoView({ block: 'nearest', inline: 'center' });
    }

    // Nút "← Quay lại" cho mọi trang chưa có sẵn link quay lại: đi từ trang khác trong web thì về trang
    // trước (giữ bộ lọc/vị trí cuộn), mở thẳng link thì về trang chủ.
    var NO_BACK = ['/', '/index.html', '/auth/login.html', '/auth/register.html'];
    function injectBackLink() {
        var main = document.querySelector('main');
        if (!main || NO_BACK.indexOf(location.pathname.toLowerCase()) >= 0 || main.querySelector('.back-link')) return;
        var link = document.createElement('a');
        link.className = 'back-link back-link-auto';
        link.href = cachedRole() === 'ADMIN' ? ADMIN_HOME : '/';
        link.textContent = '← Quay lại';
        link.addEventListener('click', function (e) {
            var fromHere = false;
            try { fromHere = !!document.referrer && new URL(document.referrer).origin === location.origin; } catch (err) { }
            if (fromHere && history.length > 1) { e.preventDefault(); history.back(); }
        });
        main.insertBefore(link, main.firstChild);
    }

    // "Đang tải..." trong khung rỗng có vòng xoay nhỏ (trang JS thay nội dung khi tải xong).
    function markLoadingStates() {
        document.querySelectorAll('.empty h3').forEach(function (h) {
            if (/^Đang tải/.test((h.textContent || '').trim())) h.parentElement.classList.add('is-loading');
        });
    }

    // Hộp thoại của hệ thống thay cho confirm()/alert()/prompt() mặc định của trình duyệt:
    //   await etomsConfirm('...', { title, okText })            -> true/false
    //   await etomsAlert('...', { title })                       -> (đóng)
    //   await etomsPrompt('...', { title, placeholder, defaultValue, required, minLength, maxLength,
    //                             pattern, patternMessage, okText }) -> chuỗi đã trim, hoặc null nếu Hủy
    var pending = null;   // { resolve, kind, opts }
    function wireConfirmModal() {
        var overlay = document.getElementById('etoms-modal');
        if (!overlay) return;
        var title = document.getElementById('etoms-modal-title');
        var msg = document.getElementById('etoms-modal-msg');
        var field = document.getElementById('etoms-modal-field');
        var input = document.getElementById('etoms-modal-input');
        var error = document.getElementById('etoms-modal-error');
        var okBtn = document.getElementById('etoms-modal-ok');
        var cancelBtn = document.getElementById('etoms-modal-cancel');

        function finish(result) {
            overlay.classList.remove('show');
            if (pending) { var p = pending; pending = null; p.resolve(result); }
        }
        function cancel() {
            if (!pending) return;
            finish(pending.kind === 'confirm' ? false : pending.kind === 'prompt' ? null : undefined);
        }
        function validate(value, o) {
            if (o.required !== false && !value) return 'Vui lòng nhập nội dung.';
            if (value && o.minLength && value.length < o.minLength) return 'Cần ít nhất ' + o.minLength + ' ký tự.';
            if (value && value.length > (o.maxLength || 500)) return 'Tối đa ' + (o.maxLength || 500) + ' ký tự.';
            if (value && o.pattern && !o.pattern.test(value)) return o.patternMessage || 'Giá trị không hợp lệ.';
            return '';
        }
        okBtn.addEventListener('click', function () {
            if (!pending) return;
            if (pending.kind !== 'prompt') { finish(pending.kind === 'confirm' ? true : undefined); return; }
            var value = input.value.trim();
            var err = validate(value, pending.opts);
            error.textContent = err;
            if (err) { input.focus(); return; }
            finish(value);
        });
        cancelBtn.addEventListener('click', cancel);
        overlay.addEventListener('click', function (e) { if (e.target === overlay) cancel(); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && overlay.classList.contains('show')) cancel(); });
        input.addEventListener('input', function () { error.textContent = ''; });

        function open(kind, message, opts) {
            opts = opts || {};
            if (pending) cancel();
            return new Promise(function (resolve) {
                pending = { resolve: resolve, kind: kind, opts: opts };
                title.textContent = opts.title || (kind === 'alert' ? 'Thông báo' : kind === 'prompt' ? 'Nhập thông tin' : 'Xác nhận');
                msg.textContent = message || '';
                field.hidden = kind !== 'prompt';
                cancelBtn.hidden = kind === 'alert';
                okBtn.textContent = opts.okText || (kind === 'alert' ? 'Đóng' : 'Đồng ý');
                error.textContent = '';
                if (kind === 'prompt') {
                    input.value = opts.defaultValue || '';
                    input.placeholder = opts.placeholder || '';
                    input.maxLength = opts.maxLength || 500;
                    input.rows = opts.multiline === false ? 1 : 3;
                }
                overlay.classList.add('show');
                (kind === 'prompt' ? input : okBtn).focus();
            });
        }

        // Dùng cho hành động điều khiển bằng JS (nút gọi fetch): await etomsConfirm('...')
        window.etomsConfirm = function (message, opts) { return open('confirm', message, opts); };
        window.etomsAlert = function (message, opts) { return open('alert', message, opts); };
        window.etomsPrompt = function (message, opts) { return open('prompt', message, opts); };

        // Link tệp riêng tư: <a href="/api/.../file" data-private-file="ten-tep.jpg"> — tải kèm token.
        document.addEventListener('click', async function (e) {
            var link = e.target.closest && e.target.closest('a[data-private-file]');
            if (!link) return;
            e.preventDefault();
            try {
                await Api.openFile(link.getAttribute('href'), link.dataset.privateFile);
            } catch (err) {
                window.etomsAlert(err.message || 'Không mở được tệp.', { title: 'Không mở được tệp' });
            }
        });

        // Vẫn hỗ trợ <form data-confirm="..."> submit thuần (không qua fetch), y hệt bản cũ.
        document.addEventListener('submit', function (e) {
            var form = e.target;
            if (form.dataset && form.dataset.confirm && !form.dataset.confirmed) {
                e.preventDefault();
                window.etomsConfirm(form.dataset.confirm).then(function (ok) {
                    if (ok) { form.dataset.confirmed = '1'; form.submit(); }
                });
            }
        });
    }

    // Đổi số này mỗi khi sửa partials/*.html để trình duyệt không dùng bản menu cũ đã nhớ.
    var PARTIAL_VERSION = 'adm3-20261118';
    async function injectPartial(url, targetId) {
        var target = document.getElementById(targetId);
        if (!target) return;
        try {
            var res = await fetch(url + '?v=' + PARTIAL_VERSION);
            target.innerHTML = await res.text();
        } catch (e) { /* thiếu nav/modal vẫn chạy tiếp được, không chặn trang */ }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        // Trang nào quên chỗ chứa hộp thoại thì tự tạo, để mọi trang đều dùng được etomsConfirm/Prompt/Alert.
        if (!document.getElementById('confirm-modal-root')) {
            var modalRoot = document.createElement('div');
            modalRoot.id = 'confirm-modal-root';
            document.body.appendChild(modalRoot);
        }
        await Promise.all([
            injectPartial('/partials/nav.html', 'site-header'),
            injectPartial('/partials/confirm-modal.html', 'confirm-modal-root')
        ]);
        if (cachedRole() === 'ADMIN') applyAdminNav(); // không để thanh menu công khai nháy lên với Admin
        applyActiveNav();
        injectBackLink();
        wirePhoneInputs();
        wireThemeToggle();
        wireConfirmModal();
        injectFooter();
        var mePromise = wireAuthWidget();
        document.dispatchEvent(new CustomEvent('etoms:layout-ready'));
        wireTournamentNav(await mePromise);
    });

    // layout.js nạp ở cuối <body>, trước script trang (defer): lúc này khung "Đang tải..." còn nguyên.
    markLoadingStates();
})();
