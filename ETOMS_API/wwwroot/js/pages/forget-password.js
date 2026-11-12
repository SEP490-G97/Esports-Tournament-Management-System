(function () {
    const F = window.AuthForms;
    const form = document.getElementById('forgot-form');
    const msg = document.getElementById('msg');
    const btn = document.getElementById('submit');
    const COOLDOWN_SECONDS = 60;

    // Thông báo cố ý KHÔNG nói email có tồn tại hay không (tránh bị dò email), nhưng nhắc lại email đã nhập
    // và gợi ý bước tiếp theo để người gõ nhầm tự phát hiện.
    function showSent(email) {
        msg.className = 'form-msg success';
        msg.textContent = '';
        msg.append('Nếu ');
        const strong = document.createElement('strong');
        strong.textContent = email;        // textContent: an toàn với ký tự đặc biệt
        msg.append(strong);
        msg.append(' có tài khoản trong hệ thống, chúng tôi đã gửi liên kết đặt lại mật khẩu (hiệu lực 30 phút). ' +
            'Chưa thấy mail? Hãy kiểm tra thư mục Spam, kiểm tra lại email đã nhập, hoặc ');
        const a = document.createElement('a');
        a.href = 'register.html';
        a.className = 'reg-link';
        a.textContent = 'đăng ký tài khoản mới';
        msg.append(a);
        msg.append('.');
        msg.hidden = false;
    }

    // Khóa nút gửi lại 60 giây để tránh bấm liên tục (server cũng giới hạn 5 lần/phút).
    function startCooldown() {
        let left = COOLDOWN_SECONDS;
        btn.disabled = true;
        btn.textContent = 'Gửi lại sau ' + left + 's';
        const timer = setInterval(() => {
            left--;
            if (left <= 0) {
                clearInterval(timer);
                btn.disabled = false;
                btn.textContent = 'Gửi lại liên kết';
            } else {
                btn.textContent = 'Gửi lại sau ' + left + 's';
            }
        }, 1000);
    }

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const email = form.email.value.trim();
        if (!F.validEmail(email)) return F.showMessage(msg, 'Email không hợp lệ. Vui lòng kiểm tra lại.', 'error');

        F.showMessage(msg, '');
        F.setBusy(btn, true, 'Đang gửi...');
        const r = await F.post('/auth/forgot-password', { email });
        F.setBusy(btn, false);

        if (r.ok) {
            showSent(email);
            startCooldown();
        } else {
            F.showMessage(msg, (r.data && r.data.message) || 'Không gửi được yêu cầu. Vui lòng thử lại.', 'error');
        }
    });
})();