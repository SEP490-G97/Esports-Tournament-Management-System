// Hồ sơ cá nhân · mục Đổi mật khẩu (góp ý nhóm 18/11 mục 21). Dùng API có sẵn POST /api/me/change-password —
// cùng luật với trang /auth/change-password.html (Utils/PasswordPolicy: 8–72 ký tự, có hoa, thường, số).
// Đổi xong máy chủ thu hồi mọi phiên đăng nhập nên đưa về trang đăng nhập.
(function () {
    function strong(p) { return p.length >= 8 && p.length <= 72 && /[A-Z]/.test(p) && /[a-z]/.test(p) && /\d/.test(p); }

    document.addEventListener('DOMContentLoaded', function () {
        var form = document.getElementById('password-form');
        if (!form) return;
        var flash = document.getElementById('pw-flash');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            var current = document.getElementById('pw-current').value;
            var next = document.getElementById('pw-new').value;
            var confirm = document.getElementById('pw-confirm').value;
            if (!current) return Ui.showAlert(flash, 'danger', 'Vui lòng nhập mật khẩu hiện tại.');
            if (!strong(next)) return Ui.showAlert(flash, 'danger', 'Mật khẩu phải từ 8 đến 72 ký tự, gồm chữ hoa, chữ thường và chữ số.');
            if (next !== confirm) return Ui.showAlert(flash, 'danger', 'Mật khẩu nhập lại không khớp.');
            if (next === current) return Ui.showAlert(flash, 'danger', 'Mật khẩu mới phải khác mật khẩu hiện tại.');
            try {
                await Api.post('/api/me/change-password', { currentPassword: current, newPassword: next });
                form.querySelectorAll('input, button').forEach(function (x) { x.disabled = true; });
                Ui.showAlert(flash, 'success', 'Đổi mật khẩu thành công. Đang chuyển về trang đăng nhập…');
                setTimeout(function () { Auth.logout('/auth/login.html'); }, 1500);
            } catch (err) {
                Ui.showAlert(flash, 'danger', err.message || 'Đổi mật khẩu thất bại.');
            }
        });
    });
})();
