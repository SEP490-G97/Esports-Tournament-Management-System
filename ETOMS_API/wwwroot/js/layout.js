// "Layout" dùng chung cho mọi trang tĩnh: chèn nav.html/confirm-modal.html, bật theme-toggle,
// hiển thị widget đăng nhập (gọi /api/dev-auth/me), và wiring modal xác nhận hành động nguy hiểm.
// Mọi trang phải có sẵn <div id="site-header"></div> và <div id="confirm-modal-root"></div>,
// và nạp theo thứ tự: api.js, ui.js, layout.js (theme-init.js nạp riêng trong <head>).
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

    async function wireAuthWidget() {
        var slot = document.getElementById('nav-auth-slot');
        if (!slot) return;
        var loggedOutHtml = '<a class="btn btn-primary" style="padding:8px 16px" href="/dev-auth/index.html">Đăng nhập</a>';
        try {
            var me = await Api.get('/api/dev-auth/me');
            if (me.isAuthenticated) {
                slot.innerHTML =
                    '<span>Xin chào, <span class="who">' + Ui.escapeHtml(me.displayName || '') + '</span></span>' +
                    '<span class="av">' + Ui.escapeHtml((me.displayName || '?').substring(0, 1)) + '</span>' +
                    '<a class="btn btn-ghost" style="padding:8px 14px" href="#" id="nav-logout">Đăng xuất</a>';
                document.getElementById('nav-logout').addEventListener('click', async function (e) {
                    e.preventDefault();
                    try { await Api.post('/api/dev-auth/logout'); } catch (err) {}
                    location.href = '/';
                });
            } else {
                slot.innerHTML = loggedOutHtml;
            }
        } catch (e) {
            slot.innerHTML = loggedOutHtml;
        }
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
