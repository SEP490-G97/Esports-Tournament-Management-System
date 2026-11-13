(function () {
    const F = window.AuthForms;
    const form = document.getElementById('reset-form');
    const msg = document.getElementById('msg');
    const btn = document.getElementById('submit');
    const again = document.getElementById('again');
    const loginLink = document.getElementById('login-link');

    const token = F.queryParam('token');
    if (!token) {
        form.hidden = true;
        F.showMessage(msg, 'Liên kết không hợp lệ. Vui lòng yêu cầu liên kết mới.', 'error');
        again.hidden = false;
        return;
    }

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const password = form.password.value;
        const pwErr = F.passwordError(password);
        if (pwErr) return F.showMessage(msg, pwErr, 'error');
        if (password !== form.confirm.value) return F.showMessage(msg, 'Mật khẩu nhập lại không khớp.', 'error');

        F.showMessage(msg, '');
        F.setBusy(btn, true);
        const r = await F.post('/auth/reset-password', { token, newPassword: password });
        F.setBusy(btn, false);

        if (r.ok) {
            form.hidden = true;
            F.showMessage(msg, r.data.message, 'success');
            loginLink.hidden = false;
            history.replaceState(null, '', location.pathname);
            return;
        }
        F.showMessage(msg, (r.data && r.data.message) || 'Đặt lại mật khẩu thất bại.', 'error');
        if (r.data && r.data.code === 'INVALID_TOKEN') { form.hidden = true; again.hidden = false; }
    });
})();