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

    // Người dùng hiện tại (đăng nhập thật, JWT) hoặc null nếu là khách. Không chuyển trang.
    // Kết quả có: isAuthenticated, userId, accountId, displayName, fullName, role.
    // Gọi nhiều lần trong 1 trang (nav + trang) chỉ hỏi máy chủ 1 lần.
    var mePromise = null;
    function currentUser() {
        if (!mePromise) {
            mePromise = (async function () {
                if (!(window.Auth && typeof Auth.tryGetMe === 'function')) return null;
                var me = await Auth.tryGetMe();
                if (!me) return null;
                me.isAuthenticated = true;
                me.userId = me.accountId = me.id;
                me.displayName = me.fullName = me.name;
                return me;
            })();
        }
        return mePromise.then(function (me) { return me ? Object.assign({}, me) : null; });
    }

    // Bắt buộc đăng nhập: khách thì chuyển sang trang đăng nhập kèm returnUrl và trả về null.
    // roles (tùy chọn): chỉ cho phép các vai trò này, sai vai trò thì về trang chủ.
    async function requireAuth(roles) {
        var me = await currentUser();
        if (!me) {
            var returnUrl = encodeURIComponent(location.pathname + location.search);
            location.href = '/auth/login.html?returnUrl=' + returnUrl;
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
        applyFieldErrors: applyFieldErrors, qs: qs, qsInt: qsInt, requireAuth: requireAuth, currentUser: currentUser,
        setFlash: setFlash, consumeFlash: consumeFlash
    };
})();
