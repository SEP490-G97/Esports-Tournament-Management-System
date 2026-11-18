// wwwroot/js/pages/login.js
(function () {
    const GOOGLE_CLIENT_ID = '702719192439-jd18c5u69n7apr4qd862kp2s87n5370f.apps.googleusercontent.com';
    const remember = document.getElementById('login-remember');
    // Trang đích sau khi đăng nhập, theo role (sửa cho đúng route của bạn)
    const ROLE_HOME = {
        PLAYER: '/',
        ORGANIZER: '/',
        ADMIN: '/',
    };

    const form = document.getElementById('login-form');
    const identity = document.getElementById('login-identity');
    const password = document.getElementById('login-password');
    const submitBtn = document.getElementById('login-submit');
    const errorBox = document.getElementById('login-error');

    // ---------- UI helpers ----------
    function showError(msg) {
        errorBox.textContent = msg;
        errorBox.style.display = 'block';
    }
    function clearError() {
        errorBox.textContent = '';
        errorBox.style.display = 'none';
    }
    function setLoading(on) {
        submitBtn.disabled = on;
        submitBtn.textContent = on ? 'Đang đăng nhập...' : 'Đăng nhập';
    }
    function showBanned(data) {
        showError('Tài khoản của bạn đã bị khóa. Lý do: ' + (data.reason || 'Không có lý do được ghi nhận'));
    }

    // Chỉ cho phép quay về đường dẫn nội bộ (chống open redirect)
    function targetAfterLogin(user) {
        const back = new URLSearchParams(location.search).get('returnUrl');
        if (back && back.startsWith('/') && !back.startsWith('//')) return back;
        return ROLE_HOME[user.role] || '/';
    }

    // ---------- Xử lý response chung cho login thường + Google ----------
    async function handleLoginResponse(res) {
        const data = await res.json().catch(() => ({}));

        if (res.ok) {
            Auth.setAccessToken(data.accessToken);
            Auth.setUser(data.user);
            location.href = targetAfterLogin(data.user);
            return;
        }
        if (res.status === 403 && data.code === 'ACCOUNT_BANNED') return showBanned(data);
        if (res.status === 403 && data.code === 'EMAIL_NOT_VERIFIED') return showError(data.message);
        if (res.status === 429) return showError('Bạn thử quá nhiều lần. Vui lòng đợi một phút rồi thử lại.');
        showError(data.message || 'Đăng nhập thất bại. Vui lòng thử lại.');
    }

    async function postJson(path, body) {
        return fetch(Auth.API_BASE + path, {
            method: 'POST',
            credentials: 'include',                 // để nhận cookie refresh_token
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
    }

    // ---------- Email / mật khẩu ----------
    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        clearError();

        const id = identity.value.trim();
        const pw = password.value;
        if (!id || !pw) return showError('Vui lòng nhập đầy đủ email và mật khẩu.');

        setLoading(true);
        try {
            // Backend hiện nhận trường "email"
            await handleLoginResponse(await postJson('/auth/login', {
                email: id,
                password: pw,
                remember: remember.checked,
            }));        } catch {
            showError('Không kết nối được máy chủ. Vui lòng thử lại.');
        } finally {
            setLoading(false);
        }
    });

    // ---------- Google ----------
    async function onGoogleCredential(response) {
        clearError();
        try {
            await handleLoginResponse(await postJson('/auth/google', { credential: response.credential }));
        } catch {
            showError('Không kết nối được máy chủ. Vui lòng thử lại.');
        }
    }

    function initGoogle(retry = 0) {
        if (!window.google || !google.accounts || !google.accounts.id) {
            if (retry < 20) return setTimeout(() => initGoogle(retry + 1), 250);  // script Google chưa tải xong
            return showError('Không tải được Google Sign-In. Hãy kiểm tra kết nối mạng.');
        }
        google.accounts.id.initialize({
            client_id: GOOGLE_CLIENT_ID,
            callback: onGoogleCredential,
        });
        google.accounts.id.renderButton(document.getElementById('google-login'), {
            type: 'standard',
            theme: 'filled_black',
            size: 'large',
            text: 'signin_with',
            shape: 'rectangular',
            width: 320,                 // = 384 (card) - 2 x 32 (padding)
            locale: 'vi',
        });
    }

    // ---------- Khởi tạo ----------
    // Bị đá từ trang khác về vì tài khoản vừa bị ban
    try {
        const raw = sessionStorage.getItem('ban_info');
        if (raw) { showBanned(JSON.parse(raw)); sessionStorage.removeItem('ban_info'); }
    } catch { }

    // Thông báo xuyên trang (vd vừa đăng ký xong) — AUTH-01.
    Ui.consumeFlash(document.getElementById('flash'));

    initGoogle();
})();