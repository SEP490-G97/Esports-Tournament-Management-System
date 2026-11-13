(function () {
    const F = window.AuthForms;
    const form = document.getElementById('register-form');
    const msg = document.getElementById('msg');
    const btn = document.getElementById('submit');

    // Không cho chọn ngày sinh trong tương lai.
    if (form.dateOfBirth) form.dateOfBirth.max = new Date().toISOString().slice(0, 10);

    function calcAge(birthIso) {
        var b = new Date(birthIso + 'T00:00:00');
        var now = new Date();
        var age = now.getFullYear() - b.getFullYear();
        var hadBirthdayThisYear = (now.getMonth() > b.getMonth()) ||
            (now.getMonth() === b.getMonth() && now.getDate() >= b.getDate());
        if (!hadBirthdayThisYear) age--;
        return age;
    }

    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        // Gamertag hiện được lưu vào Account.FullName (cột bắt buộc, 2-100 ký tự).
        const gamertag = form.gamertag.value.trim();
        const dateOfBirth = form.dateOfBirth.value;
        const email = form.email.value.trim();
        const password = form.password.value;

        if (gamertag.length < 2) return F.showMessage(msg, 'Tên người dùng phải từ 2 ký tự.', 'error');
        if (!dateOfBirth) return F.showMessage(msg, 'Vui lòng chọn ngày sinh.', 'error');
        if (calcAge(dateOfBirth) < 18) return F.showMessage(msg, 'Bạn phải đủ 18 tuổi trở lên mới được tạo tài khoản.', 'error');
        if (!F.validEmail(email)) return F.showMessage(msg, 'Email không hợp lệ.', 'error');
        const pwErr = F.passwordError(password);
        if (pwErr) return F.showMessage(msg, pwErr, 'error');
        if (password !== form.confirm.value) return F.showMessage(msg, 'Mật khẩu xác nhận không khớp.', 'error');
        if (!form.agree.checked) return F.showMessage(msg, 'Bạn cần đồng ý với Điều khoản sử dụng và Chính sách bảo mật.', 'error');

        F.showMessage(msg, '');
        F.setBusy(btn, true);
        const r = await F.post('/auth/register', { email, password, fullName: gamertag, dateOfBirth });
        F.setBusy(btn, false);

        if (r.ok) {
            form.reset();
            btn.hidden = true;
            F.showMessage(msg,
                'Đăng ký thành công. Chúng tôi đã gửi email kích hoạt tới ' + email +
                ' (liên kết có hiệu lực 24 giờ). Chưa nhận được? Vào trang xác minh để gửi lại.', 'success');
            return;
        }
        if (r.data && r.data.code === 'EMAIL_TAKEN') {
            F.showMessage(msg, 'Email này đã được đăng ký. Nếu chưa kích hoạt, hãy mở trang xác minh email để gửi lại liên kết.', 'error');
            return;
        }
        F.showMessage(msg, (r.data && r.data.message) || 'Đăng ký thất bại. Vui lòng thử lại.', 'error');
    });
})();
