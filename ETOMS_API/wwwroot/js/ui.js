// Tiện ích dùng chung cho mọi trang tĩnh: thoát HTML, hiện alert/toast, gán lỗi theo field.
var Ui = (function () {
    function escapeHtml(s) {
        if (s === null || s === undefined) return '';
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function safeUrl(value) {
        if (!value) return '';
        try {
            var url = new URL(value, location.origin);
            return url.protocol === 'http:' || url.protocol === 'https:' ? url.href : '';
        } catch (e) {
            return '';
        }
    }

    // containerEl: phần tử chứa alert (thường là 1 <div id="flash"></div> đầu trang/khối).
    function showAlert(containerEl, type, message) {
        if (!containerEl) return;
        containerEl.innerHTML = '<div class="alert alert-' + (type === 'success' ? 'success' : 'danger') + '">' + escapeHtml(message) + '</div>';
    }

    function clearAlert(containerEl) {
        if (containerEl) containerEl.innerHTML = '';
    }

    // form: phần tử <form>/<div> chứa các span lỗi field, mỗi span có data-field-error="TenField".
    function clearFieldErrors(form) {
        if (!form) return;
        form.querySelectorAll('[data-field-error]').forEach(function (el) { el.textContent = ''; });
    }

    function applyFieldErrors(form, fieldErrors) {
        if (!form || !fieldErrors) return;
        Object.keys(fieldErrors).forEach(function (key) {
            var el = form.querySelector('[data-field-error="' + key + '"]');
            if (!el) return;
            var v = fieldErrors[key];
            el.textContent = Array.isArray(v) ? v[0] : v;
        });
    }

    function qs(name) {
        return new URLSearchParams(location.search).get(name);
    }

    function qsInt(name) {
        var v = qs(name);
        var n = v !== null ? parseInt(v, 10) : NaN;
        return isNaN(n) ? null : n;
    }

    // Bắt buộc đăng nhập. Giữ cả 2 nguồn để nhóm test song song:
    //   1. Đăng nhập thật bằng JWT (AUTH-02) nếu trang có nạp auth.js và đã đăng nhập.
    //   2. Đăng nhập thử /dev-auth (session, chỉ Development).
    // Chưa đăng nhập thì chuyển sang trang đăng nhập kèm returnUrl và trả về null.
    // roles (tùy chọn): chỉ cho phép các vai trò này, sai vai trò thì về trang chủ.
    // Kết quả luôn có: isAuthenticated, userId, accountId, displayName, fullName, role.
    async function requireAuth(roles) {
        var hasJwt = !!(window.Auth && typeof Auth.tryGetMe === 'function');
        var me = null;
        if (hasJwt) {
            var jwtMe = await Auth.tryGetMe();
            if (jwtMe) {
                me = jwtMe;
                me.isAuthenticated = true;
                me.userId = me.accountId = me.id;
                me.displayName = me.fullName = me.name;
            }
        }
        if (!me) {
            try { me = await Api.get('/api/dev-auth/me'); } catch (e) { me = null; }
            if (me && me.isAuthenticated) {
                me.accountId = me.userId;
                me.fullName = me.displayName;
            } else {
                me = null;
            }
        }
        if (!me) {
            var returnUrl = encodeURIComponent(location.pathname + location.search);
            location.href = (hasJwt ? '/auth/login.html' : '/dev-auth/index.html') + '?returnUrl=' + returnUrl;
            return null;
        }
        if (roles && roles.length && roles.indexOf(me.role) < 0) { location.href = '/'; return null; }
        return me;
    }

    // Flash message xuyên trang (thay TempData["ThanhCong"]/["LoiChung"] cũ): đặt trước khi
    // điều hướng sang trang khác (vd sau khi Disband), đọc 1 lần rồi tự xoá ở trang đích.
    function setFlash(message, type) {
        try { sessionStorage.setItem('etoms-flash', JSON.stringify({ message: message, type: type || 'success' })); } catch (e) {}
    }

    function consumeFlash(containerEl) {
        try {
            var raw = sessionStorage.getItem('etoms-flash');
            if (!raw) return;
            sessionStorage.removeItem('etoms-flash');
            var f = JSON.parse(raw);
            showAlert(containerEl, f.type, f.message);
        } catch (e) {}
    }

    return {
        escapeHtml: escapeHtml, safeUrl: safeUrl, showAlert: showAlert, clearAlert: clearAlert, clearFieldErrors: clearFieldErrors,
        applyFieldErrors: applyFieldErrors, qs: qs, qsInt: qsInt, requireAuth: requireAuth,
        setFlash: setFlash, consumeFlash: consumeFlash
    };
})();
