(function () {
    const nativeFetch = window.fetch.bind(window);
    const API_BASE = '';
    const LOGIN_URL = '/auth/login.html';
    const TOKEN_KEY = 'etoms_access_token';

    // Access token giữ trong localStorage (dùng chung mọi tab): site nhiều trang, mỗi lần chuyển
    // trang/mở tab mới là tải JS mới. Refresh token dùng 1 lần (rotation), nên càng ít lần gọi
    // /auth/refresh càng tốt — token còn hạn (15 phút) thì mọi trang, mọi tab dùng thẳng.
    let accessToken = null;
    try { accessToken = localStorage.getItem(TOKEN_KEY) || null; } catch { }
    let refreshing = null;                  // gộp nhiều lần refresh đồng thời thành 1

    function setAccessToken(t) {
        accessToken = t;
        try { if (t) localStorage.setItem(TOKEN_KEY, t); else localStorage.removeItem(TOKEN_KEY); } catch { }
    }
    function getAccessToken() { return accessToken; }

    function setUser(u) {
        try { if (u) localStorage.setItem('user', JSON.stringify(u)); else localStorage.removeItem('user'); } catch { }
    }
    function getUser() {
        try { return JSON.parse(localStorage.getItem('user')); } catch { return null; }
    }

    async function refresh() {
        // Refresh token dùng 1 lần (rotation), nên nhiều request cùng lúc phải dùng chung 1 lần refresh
        if (!refreshing) {
            const before = accessToken;
            refreshing = (async () => {
                const res = await fetch(API_BASE + '/auth/refresh', {
                    method: 'POST', credentials: 'include'
                });
                const data = await res.json().catch(() => ({}));
                if (!res.ok) {
                    // Tab khác có thể vừa refresh xong (cookie đã xoay vòng) — dùng token nó để lại.
                    let shared = null;
                    try { shared = localStorage.getItem(TOKEN_KEY); } catch { }
                    if (shared && shared !== before) { accessToken = shared; return accessToken; }
                    throw Object.assign(new Error(data.message || 'Phiên đăng nhập hết hạn'),
                        { status: res.status, data });
                }
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
        setUser(null);
        location.href = to || LOGIN_URL;
    }


    window.Auth = {
        API_BASE, setAccessToken, getAccessToken, setUser, getUser,
        refresh, apiFetch, requireAuth, tryGetMe, logout
    };
})();