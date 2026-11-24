// AUTH-03: đặt lại mật khẩu bằng liên kết trong email. Liên kết chỉ dùng MỘT lần (máy chủ đánh dấu đã dùng khi đổi
// mật khẩu thành công). Trang kiểm liên kết NGAY KHI MỞ: đã dùng / hết hạn / sai thì hiện trang lỗi thay vì form.
(function () {
    const F = window.AuthForms;
    const form = document.getElementById('reset-form');
    const msg = document.getElementById('msg');
    const btn = document.getElementById('submit');
    const again = document.getElementById('again');
    const loginLink = document.getElementById('login-link');
    const title = document.querySelector('.reg-title');
    const sub = document.querySelector('.reg-sub');

    // Trang lỗi: ẩn form, đổi tiêu đề theo lý do, chỉ còn nút yêu cầu liên kết mới.
    function showLinkError(status, text) {
        form.hidden = true;
        title.textContent = status === 'USED' ? 'Liên kết đã được sử dụng'
            : status === 'EXPIRED' ? 'Liên kết đã hết hạn' : 'Liên kết không hợp lệ';
        sub.textContent = 'Không thể đặt lại mật khẩu bằng liên kết này.';
        F.showMessage(msg, text || 'Liên kết đặt lại mật khẩu không hợp lệ. Vui lòng yêu cầu liên kết mới.', 'error');
        msg.hidden = false;
        form.insertAdjacentElement('afterend', msg);   // thông báo vẫn hiện khi form ẩn
        again.hidden = false;
        loginLink.hidden = false;
        history.replaceState(null, '', location.pathname);   // bỏ token khỏi thanh địa chỉ
    }

    const token = F.queryParam('token');
    if (!token) { showLinkError('INVALID'); return; }

    form.hidden = true;   // chờ kiểm liên kết xong mới cho nhập
    (async function () {
        const r = await F.post('/auth/reset-password/check', { token });
        if (!r.ok) {
            // Mất mạng / quá nhiều lần thử: vẫn cho nhập, máy chủ kiểm lại khi gửi.
            form.hidden = false;
            if (r.status === 429) F.showMessage(msg, 'Thao tác quá nhanh, vui lòng thử lại sau ít phút.', 'error');
            return;
        }
        if (r.data.status !== 'VALID') { showLinkError(r.data.status, r.data.message); return; }
        form.hidden = false;
        form.password.focus();
    })();

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
            title.textContent = 'Đã đổi mật khẩu';
            sub.textContent = 'Liên kết này đã hết hiệu lực. Mọi phiên đăng nhập cũ đã bị đăng xuất.';
            F.showMessage(msg, r.data.message, 'success');
            form.insertAdjacentElement('afterend', msg);
            loginLink.hidden = false;
            history.replaceState(null, '', location.pathname);
            return;
        }
        if (r.data && r.data.code === 'INVALID_TOKEN') {
            showLinkError('USED', 'Liên kết đặt lại mật khẩu đã hết hạn hoặc đã được sử dụng. Vui lòng yêu cầu liên kết mới.');
            return;
        }
        F.showMessage(msg, 'Đổi mật khẩu không thành công: ' + ((r.data && r.data.message) || 'vui lòng thử lại.'), 'error');
    });
})();
