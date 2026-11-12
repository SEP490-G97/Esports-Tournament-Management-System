(function () {
    const F = window.AuthForms;
    const msg = document.getElementById('msg');
    const resendForm = document.getElementById('resend-form');
    const loginLink = document.getElementById('login-link');
    const resendBtn = document.getElementById('resend');

    function showResend(prefillEmail) {
        resendForm.hidden = false;
        if (prefillEmail) resendForm.email.value = prefillEmail;
    }

    resendForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        const email = resendForm.email.value.trim();
        if (!F.validEmail(email)) return F.showMessage(msg, 'Email không hợp lệ.', 'error');
        F.setBusy(resendBtn, true);
        const r = await F.post('/auth/resend-verification', { email });
        F.setBusy(resendBtn, false);
        F.showMessage(msg, (r.data && r.data.message) || 'Đã xử lý yêu cầu.', r.ok ? 'success' : 'error');
    });

    (async function init() {
        const token = F.queryParam('token');
        const resend = F.queryParam('resend');

        // Vào từ trang đăng nhập (EMAIL_NOT_VERIFIED): chỉ hiện form gửi lại.
        if (!token) {
            F.showMessage(msg, resend
                ? 'Tài khoản của bạn chưa được xác minh email.'
                : 'Vui lòng mở liên kết trong email xác minh, hoặc yêu cầu gửi lại bên dưới.', '');
            showResend(resend);
            return;
        }

        // Token trong URL chỉ được POST khi trang tải, tránh bộ quét link của hộp thư làm cháy token.
        F.showLoading(msg, 'Đang xác minh email...');
        const r = await F.post('/auth/verify-email', { token });
        if (r.ok) {
            F.showMessage(msg, 'Xác minh email thành công. Bây giờ bạn có thể đăng nhập.', 'success');
            loginLink.hidden = false;
            history.replaceState(null, '', location.pathname); // xóa token khỏi thanh địa chỉ
        } else {
            F.showMessage(msg, (r.data && r.data.message) || 'Xác minh thất bại.', 'error');
            showResend();
            loginLink.hidden = false;
        }
    })();
})();