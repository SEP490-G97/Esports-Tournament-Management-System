// wwwroot/js/pages/login.js
(function () {
    const GOOGLE_CLIENT_ID = '702719192439-jd18c5u69n7apr4qd862kp2s87n5370f.apps.googleusercontent.com';
    const remember = document.getElementById('login-remember');
    // Trang đích sau khi đăng nhập, theo role (sửa cho đúng route của bạn)
    const ROLE_HOME = {
        PLAYER: '/',
        ORGANIZER: '/',
        ADMIN: '/admin/index.html',   // Admin chỉ dùng khu quản trị
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
        // Admin chỉ quay về trang trong khu quản trị / đơn vị tổ chức; trang khác thì vào Bảng quản trị.
        const adminOk = !back || user.role !== 'ADMIN' || /^\/(admin|organization)\//i.test(back);
        if (back && back.startsWith('/') && !back.startsWith('//') && adminOk) return back;
        return ROLE_HOME[user.role] || '/';
    }

    // ---------- Xử lý response chung cho login thường + Google ----------
    async function handleLoginResponse(res) {
        const data = await res.json().catch(() => ({}));

        if (res.ok) {
            // Không ghi nhớ => phiên riêng của tab (pendingTab); ghi nhớ => phiên dùng chung mọi tab.
            Auth.beginSession(pendingTab, data.accessToken, data.user);
            // Đang chuyển trang: khóa nút, không cho gửi đăng nhập lần 2 (lần 2 sẽ thu hồi phiên vừa tạo — nếu yêu cầu
            // bị ngắt giữa chừng do chuyển trang thì trình duyệt giữ token đã bị thu hồi và bị đăng xuất sau đó).
            navigating = true;
            submitBtn.disabled = true;
            submitBtn.textContent = 'Đang vào trang…';
            location.href = targetAfterLogin(data.user);
            return;
        }
        var msg;
        if (res.status === 403 && data.code === 'ACCOUNT_BANNED') msg = 'Tài khoản của bạn đã bị khóa. Lý do: ' + (data.reason || 'Không có lý do được ghi nhận');
        else if (res.status === 403 && data.code === 'EMAIL_NOT_VERIFIED') msg = data.message;
        else if (res.status === 429) msg = 'Bạn thử quá nhiều lần. Vui lòng đợi một phút rồi thử lại.';
        else msg = data.message || 'Đăng nhập thất bại. Vui lòng thử lại.';
        // Đang có phiên mà đăng nhập tài khoản khác thất bại: phiên cũ vẫn giữ nguyên, nói rõ để khỏi tưởng đã bị đăng xuất.
        if (currentMe) msg += ' Bạn vẫn đang đăng nhập bằng tài khoản ' + (currentMe.fullName || currentMe.email) + '.';
        showError(msg);
    }

    // ---------- Đã đăng nhập mà mở trang Đăng nhập ----------
    // Không tự chuyển trang (tránh vòng lặp khi bị đưa về đây vì thiếu quyền), mà báo rõ đang dùng tài khoản nào:
    // "Tiếp tục" về trang định vào / trang chủ, hoặc "Đăng nhập tài khoản khác" (phiên cũ chỉ bị thay khi đăng nhập
    // mới thành công — máy chủ thu hồi refresh token cũ lúc đó).
    let currentMe = null;
    const sessionBox = document.getElementById('login-session');
    const loginParts = ['login-form', 'login-divider', 'google-login', 'login-foot'].map(function (id) { return document.getElementById(id); });
    function showLoginParts(on) { loginParts.forEach(function (el) { if (el) el.hidden = !on; }); }

    function showSession(me) {
        const esc = Ui.escapeHtml;
        const roleLabel = { PLAYER: 'Người chơi', ORGANIZER: 'Ban tổ chức', ADMIN: 'Quản trị hệ thống' }[me.role] || me.role;
        sessionBox.innerHTML =
            '<div class="login-session-who"><span class="login-session-av">' + esc((me.fullName || me.email || '?').trim().slice(0, 1).toUpperCase()) + '</span>' +
            '<div><div class="login-session-name">' + esc(me.fullName || me.email) + '</div>' +
            '<div class="login-session-sub">' + esc(me.email || '') + ' · ' + esc(roleLabel) + '</div></div></div>' +
            '<p class="login-session-note">Bạn đang đăng nhập bằng tài khoản này.</p>' +
            '<a class="login-submit login-continue" id="session-continue" href="' + esc(targetAfterLogin(me)) + '">Tiếp tục với tài khoản này</a>' +
            '<button type="button" class="login-switch" id="session-switch">Đăng nhập tài khoản khác</button>' +
            '<button type="button" class="login-switch" id="session-logout">Đăng xuất</button>';
        sessionBox.hidden = false;
        showLoginParts(false);
        document.getElementById('session-switch').addEventListener('click', function () {
            sessionBox.hidden = true;
            showLoginParts(true);
            const hint = document.createElement('p');
            hint.className = 'login-session-hint';
            hint.textContent = 'Đang đăng nhập bằng ' + (me.fullName || me.email) + '. Đăng nhập tài khoản khác thành công thì tài khoản hiện tại sẽ được đăng xuất.';
            form.insertAdjacentElement('beforebegin', hint);
            identity.focus();
        });
        document.getElementById('session-logout').addEventListener('click', function () { Auth.logout('/auth/login.html'); });
    }

    async function checkSession() {
        // Có dấu vết phiên (token/người dùng đã lưu) thì ẩn form trong lúc kiểm, tránh nháy form rồi mới đổi.
        let hint = null;
        try { hint = Auth.getAccessToken() || Auth.getUser(); } catch { }
        if (hint) showLoginParts(false);
        const me = await Ui.currentUser();
        if (me && me.isAuthenticated !== false && (me.userId || me.accountId || me.email)) {
            currentMe = me;
            showSession(me);
        } else {
            showLoginParts(true);
        }
    }

    async function postJson(path, body, tab) {
        return fetch(Auth.API_BASE + path, {
            method: 'POST',
            credentials: 'include',                 // để nhận cookie refresh_token
            headers: Object.assign({ 'Content-Type': 'application/json' }, tab ? { 'X-Etoms-Tab': tab } : {}),
            body: JSON.stringify(body),
        });
    }

    // ---------- Email / mật khẩu ----------
    // pendingTab: mã tab gửi kèm lần đăng nhập này (null = Ghi nhớ đăng nhập, phiên dùng chung mọi tab).
    let busy = false, navigating = false, pendingTab = null;
    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (busy || navigating) return;            // chặn bấm/Enter liên tục
        clearError();

        const id = identity.value.trim();
        const pw = password.value;
        if (!id || !pw) return showError('Vui lòng nhập đầy đủ email và mật khẩu.');

        busy = true;
        pendingTab = Auth.loginTabId(remember.checked);
        setLoading(true);
        try {
            // Backend hiện nhận trường "email"
            await handleLoginResponse(await postJson('/auth/login', {
                email: id,
                password: pw,
                remember: remember.checked,
            }, pendingTab));
        } catch {
            showError('Không kết nối được máy chủ. Vui lòng thử lại.');
        } finally {
            busy = false;
            if (!navigating) setLoading(false);   // thành công thì giữ nút khóa tới khi trang mới mở
        }
    });

    // ---------- Google ----------
    async function onGoogleCredential(response) {
        if (busy || navigating) return;
        busy = true;
        // Google cũng theo ô "Ghi nhớ đăng nhập": bỏ chọn => phiên riêng của tab này.
        pendingTab = Auth.loginTabId(remember.checked);
        clearError();
        try {
            await handleLoginResponse(await postJson('/auth/google', { credential: response.credential, remember: remember.checked }, pendingTab));
        } catch {
            showError('Không kết nối được máy chủ. Vui lòng thử lại.');
        } finally {
            busy = false;
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
    checkSession();
})();