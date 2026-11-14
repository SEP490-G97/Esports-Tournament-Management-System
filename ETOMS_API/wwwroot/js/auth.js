(function () {
    const nativeFetch = window.fetch.bind(window);
    const API_BASE = '';
    const LOGIN_URL = '/auth/login.html';
    const TOKEN_KEY = 'etoms_access_token';

    // Access token giữ trong sessionStorage (không chỉ biến trong bộ nhớ): mỗi lần chuyển
    // trang (site này không phải SPA) là 1 lượt tải JS mới, biến trong bộ nhớ sẽ mất. Trước đây
    // mỗi trang phải refresh lại — mà refresh token dùng 1 lần (rotation) + dùng lại token cũ sẽ
    // bị coi là bị đánh cắp và THU HỒI TOÀN BỘ token của tài khoản (xem RefreshTokenService.
    // ConsumeAsync) — 2 trang gọi refresh gần nhau (vd nav và script của trang cùng lúc, hoặc bấm
    // link trước khi trang trước kịp nhận cookie mới) dễ làm "đăng xuất" cả phiên dù vừa đăng nhập
    // xong. Lưu access token lại để các trang sau còn hạn (15 phút) dùng thẳng, không cần refresh.
    let accessToken = null;
    try { accessToken = sessionStorage.getItem(TOKEN_KEY) || null; } catch { }
    let refreshing = null;                  // gộp nhiều lần refresh đồng thời thành 1

    function setAccessToken(t) {
        accessToken = t;
        try { if (t) sessionStorage.setItem(TOKEN_KEY, t); else sessionStorage.removeItem(TOKEN_KEY); } catch { }
    }
    function getAccessToken() { return accessToken; }

    function setUser(u) {
        try { sessionStorage.setItem('user', JSON.stringify(u)); } catch { }
    }
    function getUser() {
        try { return JSON.parse(sessionStorage.getItem('user')); } catch { return null; }
    }

    async function refresh() {
        // Refresh token dùng 1 lần (rotation), nên nhiều request cùng lúc phải dùng chung 1 lần refresh
        if (!refreshing) {
            refreshing = (async () => {
                const res = await fetch(API_BASE + '/auth/refresh', {
                    method: 'POST', credentials: 'include'
                });
                const data = await res.json().catch(() => ({}));
                if (!res.ok) throw Object.assign(new Error(data.message || 'Phiên đăng nhập hết hạn'),
                    { status: res.status, data });
                setAccessToken(data.accessToken);
                return accessToken;
            })().finally(() => { refreshing = null; });
        }
        return refreshing;
    }

    function redirectToLogin(banData) {
        setAccessToken(null);
        if (banData) {
            try { sessionStorage.setItem('ban_info', JSON.stringify(banData)); } catch { }
        }
        const back = encodeURIComponent(location.pathname + location.search);
        location.href = `${LOGIN_URL}?returnUrl=${back}`;
    }

    // Dùng thay cho fetch ở mọi API cần đăng nhập
    async function apiFetch(path, options = {}) {
        const send = () => fetch(API_BASE + path, {
            ...options,
            credentials: 'include',
            headers: {
                ...(options.body && !(options.body instanceof FormData)
                    ? { 'Content-Type': 'application/json' } : {}),
                ...(options.headers || {}),
                ...(accessToken ? { Authorization: 'Bearer ' + accessToken } : {}),
            },
        });

        let res = await send();

        if (res.status === 401) {                    // token hết hạn -> refresh rồi thử lại 1 lần
            try { await refresh(); }
            catch (e) { redirectToLogin(e.data && e.data.code === 'ACCOUNT_BANNED' ? e.data : null); throw e; }
            res = await send();
        }
        if (res.status === 403) {
            const data = await res.clone().json().catch(() => ({}));
            if (data.code === 'ACCOUNT_BANNED') { redirectToLogin(data); throw new Error('ACCOUNT_BANNED'); }
        }
        return res;
    }

    // Gọi đầu mỗi trang cần đăng nhập. Tùy chọn: chỉ cho phép một số role.
    async function requireAuth(roles) {
        try { if (!accessToken) await refresh(); }
        catch (e) { redirectToLogin(e.data && e.data.code === 'ACCOUNT_BANNED' ? e.data : null); return null; }

        const res = await apiFetch('/api/me');
        if (!res.ok) { redirectToLogin(); return null; }
        const me = await res.json();
        setUser(me);

        if (roles && !roles.includes(me.role)) { location.href = '/'; return null; }
        return me;
    }

    // Kiểm tra nhẹ cho widget trên header: không chuyển hướng. Trả về user hoặc null.
    // Token đang nhớ (sessionStorage) có thể đã hết hạn từ lần trước — nếu /api/me báo 401 thì
    // refresh rồi thử lại đúng 1 lần, giống apiFetch, thay vì vội kết luận "chưa đăng nhập".
    async function tryGetMe() {
        const callMe = () => nativeFetch(API_BASE + '/api/me', {
            credentials: 'include',
            headers: accessToken ? { Authorization: 'Bearer ' + accessToken } : {},
        });
        try {
            if (!accessToken) await refresh();
            let res = await callMe();
            if (res.status === 401) {
                await refresh();
                res = await callMe();
            }
            if (!res.ok) return null;
            const me = await res.json();
            setUser(me);
            return me;
        } catch (e) { return null; }
    }

    async function logout(to) {
        try { await nativeFetch(API_BASE + '/auth/logout', { method: 'POST', credentials: 'include' }); } catch { }
        setAccessToken(null);
        try { sessionStorage.removeItem('user'); } catch { }
        location.href = to || LOGIN_URL;
    }


    window.Auth = {
        API_BASE, setAccessToken, getAccessToken, setUser, getUser,
        refresh, apiFetch, requireAuth, tryGetMe, logout
    };
})();