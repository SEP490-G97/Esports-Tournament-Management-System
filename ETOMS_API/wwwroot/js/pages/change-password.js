(async function () {
    const F = window.AuthForms;
    const form = document.getElementById('change-form');
    const msg = document.getElementById('msg');
    const btn = document.getElementById('submit');

    // Trang yêu cầu đăng nhập; chưa đăng nhập thì auth.js chuyển hướng về login kèm returnUrl.
    // Không dựa vào giá trị trả về của requireAuth (có thể không trả gì khi đã đăng nhập).
    await Auth.requireAuth();

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const current = form.current.value;
        const password = form.password.value;

        if (!current) return F.showMessage(msg, 'Vui lòng nhập mật khẩu hiện tại.', 'error');
        const pwErr = F.passwordError(password);
        if (pwErr) return F.showMessage(msg, pwErr, 'error');
        if (password !== form.confirm.value) return F.showMessage(msg, 'Mật khẩu nhập lại không khớp.', 'error');
        if (password === current) return F.showMessage(msg, 'Mật khẩu mới phải khác mật khẩu hiện tại.', 'error');

        F.showMessage(msg, '');
        F.setBusy(btn, true);

        let res;
        try {
            res = await Auth.apiFetch('/api/me/change-password', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ currentPassword: current, newPassword: password })
            });
        } catch {
            F.setBusy(btn, false);
            return F.showMessage(msg, 'Không kết nối được máy chủ. Vui lòng thử lại.', 'error');
        }

        let data = null;
        try { data = await res.json(); } catch { /* rỗng */ }
        F.setBusy(btn, false);

        if (res.ok) {
            form.hidden = true;
            F.showMessage(msg, 'Đổi mật khẩu thành công. Đang chuyển về trang đăng nhập...', 'success');
            // Mọi refresh token đã bị thu hồi ở server; logout dọn access token trong bộ nhớ và cookie.
            setTimeout(() => Auth.logout('/auth/login.html'), 1500);
            return;
        }
        F.showMessage(msg, (data && data.message) || 'Đổi mật khẩu thất bại.', 'error');
    });
})();