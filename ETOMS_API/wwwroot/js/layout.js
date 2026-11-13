// Layout dùng chung cho mọi trang tĩnh: chèn nav.html/confirm-modal.html, theme-toggle,
// widget đăng nhập, modal xác nhận. Nạp theo thứ tự: api.js, ui.js, layout.js.
(function () {
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

    // Đọc người dùng hiện tại theo 2 nguồn (để nhóm test song song):
    //   1. Đăng nhập thật bằng JWT (AUTH-02, auth.js) nếu trang có nạp auth.js.
    //   2. Đăng nhập thử /dev-auth (session, chỉ Development).
    // Trả về cùng một dạng { isAuthenticated, userId, displayName, role, source }.
    async function resolveCurrentUser() {
        if (window.Auth && typeof Auth.tryGetMe === 'function') {
            var jwtMe = await Auth.tryGetMe();                 // không chuyển hướng khi chưa đăng nhập
            if (jwtMe) {
                return {
                    isAuthenticated: true,
                    userId: jwtMe.id || jwtMe.accountId,
                    displayName: jwtMe.name || jwtMe.fullName || jwtMe.email || '',
                    role: jwtMe.role,
                    source: 'jwt'
                };
            }
        }
        try {
            var devMe = await Api.get('/api/dev-auth/me');
            if (devMe && devMe.isAuthenticated) { devMe.source = 'dev'; return devMe; }
        } catch (e) { /* ngoài Development endpoint trả 404 */ }
        return { isAuthenticated: false };
    }

    async function wireAuthWidget() {
        var slot = document.getElementById('nav-auth-slot');
        if (!slot) return;
        var loggedOutHtml = '<a class="btn btn-primary nav-auth-login" href="/auth/login.html">Đăng nhập</a>';
        try {
            var me = await resolveCurrentUser();
            if (me.isAuthenticated) {
                renderUserDropdown(slot, me);
                await loadMyTeam(me);
            } else {
                slot.innerHTML = loggedOutHtml;
            }
            applyRoleVisibility(me);
        } catch (e) {
            slot.innerHTML = loggedOutHtml;
            applyRoleVisibility({ isAuthenticated: false });
        }
    }

    // Khung "Xin chào, X" giờ là 1 dropdown: bấm avatar/tên để mở, bên trong có "Đội của tôi"
    // (nối thẳng tới team/details.html?id=, hoặc báo "Không có đội" nếu chưa ở đội nào) + Đăng xuất.
    function renderUserDropdown(slot, me) {
        slot.innerHTML =
            '<div class="nav-user-dd" id="nav-user-dd">' +
            '  <button type="button" class="nav-user-trigger" id="nav-user-trigger" aria-expanded="false">' +
            '    <span class="av">' + Ui.escapeHtml((me.displayName || '?').substring(0, 1).toUpperCase()) + '</span>' +
            '    <span>Xin chào, <span class="who">' + Ui.escapeHtml(me.displayName || '') + '</span></span>' +
            '    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" class="nav-user-caret"><path d="M6 9l6 6 6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>' +
            '  </button>' +
            '  <div class="nav-user-menu" id="nav-user-menu">' +
            '    <div class="nav-user-menu-item nav-user-myteam" id="nav-user-myteam">Đang tải đội…</div>' +
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
        document.getElementById('nav-logout').addEventListener('click', async function (e) {
            e.preventDefault();
            if (me.source === 'jwt') {
                Auth.logout('/');                              // thu hồi refresh token, xóa cookie, về trang chủ
                return;
            }
            try { await Api.post('/api/dev-auth/logout'); } catch (err) {}
            location.href = '/';
        });
    }

    async function loadMyTeam(me) {
        var box = document.getElementById('nav-user-myteam');
        if (!box) return;
        try {
            var team;
            if (me && me.source === 'jwt') {
                var res = await Auth.apiFetch('/api/teams/mine');
                team = res.ok ? await res.json() : null;
            } else {
                team = await Api.get('/api/teams/mine');
            }
            if (team && team.teamId) {
                box.innerHTML = 'Đội của tôi: <a href="/team/details.html?id=' + team.teamId + '">' +
                    Ui.escapeHtml(team.name) + '</a>';
            } else {
                box.textContent = 'Đội của tôi: Không có';
            }
        } catch (e) {
            box.textContent = 'Đội của tôi: Không có';
        }
    }

    // Chỉ Ban tổ chức/Admin thấy mục Tranh chấp. Link ẩn sẵn trong nav.html, chỗ này chỉ bật
    // hiện lên nếu đúng quyền — quyền thật vẫn chặn ở API dù có thấy nút hay không.
    function applyRoleVisibility(me) {
        var canSeeDisputes = !!me.isAuthenticated && (me.role === 'ORGANIZER' || me.role === 'ADMIN');
        var link = document.querySelector('#etomsNavCollapse nav a[data-nav="result"]');
        if (link) link.style.display = canSeeDisputes ? '' : 'none';
    }

    var pendingResolve = null;
    function wireConfirmModal() {
        var overlay = document.getElementById('etoms-modal');
        if (!overlay) return;
        var msg = document.getElementById('etoms-modal-msg');
        var okBtn = document.getElementById('etoms-modal-ok');
        var cancelBtn = document.getElementById('etoms-modal-cancel');

        function close(result) {
            overlay.classList.remove('show');
            if (pendingResolve) { var r = pendingResolve; pendingResolve = null; r(result); }
        }
        okBtn.addEventListener('click', function () { close(true); });
        cancelBtn.addEventListener('click', function () { close(false); });
        overlay.addEventListener('click', function (e) { if (e.target === overlay) close(false); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape') close(false); });

        // Dùng cho hành động điều khiển bằng JS (nút gọi fetch): await etomsConfirm('...')
        window.etomsConfirm = function (message) {
            return new Promise(function (resolve) {
                pendingResolve = resolve;
                msg.textContent = message;
                overlay.classList.add('show');
            });
        };

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

    async function injectPartial(url, targetId) {
        var target = document.getElementById(targetId);
        if (!target) return;
        try {
            var res = await fetch(url);
            target.innerHTML = await res.text();
        } catch (e) { /* thiếu nav/modal vẫn chạy tiếp được, không chặn trang */ }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        await Promise.all([
            injectPartial('/partials/nav.html', 'site-header'),
            injectPartial('/partials/confirm-modal.html', 'confirm-modal-root')
        ]);
        applyActiveNav();
        wireThemeToggle();
        wireConfirmModal();
        wireAuthWidget();
        document.dispatchEvent(new CustomEvent('etoms:layout-ready'));
    });
})();
