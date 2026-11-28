(function () {
    const nativeFetch = window.fetch.bind(window);
    const API_BASE = '';
    const LOGIN_URL = '/auth/login.html';
    const TOKEN_KEY = 'etoms_access_token';
    const USER_KEY = 'user';
    const TAB_KEY = 'etoms_tab_id';

    // Hai kiểu phiên (góp ý 28/11):
    // - "Ghi nhớ đăng nhập": access token + người dùng ở localStorage, refresh token ở cookie chung "refresh_token"
    //   => dùng chung mọi tab, giữ khi mở lại trình duyệt (như trước).
    // - Không ghi nhớ: phiên RIÊNG của tab. Tab có mã riêng (sessionStorage, không chia sẻ sang tab khác); token +
    //   người dùng ở sessionStorage; mọi lần refresh/đăng xuất gửi header X-Etoms-Tab để máy chủ đọc đúng cookie
    //   "refresh_token_<mã tab>". Tab mới (kể cả mọi tab ẩn danh) chưa có mã => chưa đăng nhập; mỗi tab một tài khoản.
    // Refresh token dùng 1 lần (rotation), nên càng ít lần gọi /auth/refresh càng tốt — token còn hạn (15 phút) thì dùng thẳng.
    try { localStorage.removeItem('etoms_session_only'); document.cookie = 'etoms_alive=; path=/; max-age=0'; } catch { }   // dọn cờ của bản trước

    function tabId() { try { return sessionStorage.getItem(TAB_KEY) || null; } catch { return null; } }
    function store() { return tabId() ? sessionStorage : localStorage; }
    function tabHeaders(id) { const t = id === undefined ? tabId() : id; return t ? { 'X-Etoms-Tab': t } : {}; }
    function newTabId() {
        const b = new Uint8Array(16);
        (window.crypto || window.msCrypto).getRandomValues(b);
        return Array.from(b, function (x) { return x.toString(16).padStart(2, '0'); }).join('');
    }

    let accessToken = null;
    try { accessToken = store().getItem(TOKEN_KEY) || null; } catch { }
    let refreshing = null;                  // gộp nhiều lần refresh đồng thời thành 1

    function setAccessToken(t) {
        accessToken = t;
        try { const s = store(); if (t) s.setItem(TOKEN_KEY, t); else s.removeItem(TOKEN_KEY); } catch { }
    }
    function getAccessToken() { return accessToken; }

    function setUser(u) {
        try { const s = store(); if (u) s.setItem(USER_KEY, JSON.stringify(u)); else s.removeItem(USER_KEY); } catch { }
    }
    function getUser() {
        try { return JSON.parse(store().getItem(USER_KEY)); } catch { return null; }
    }

    // Trang Đăng nhập: mã tab gửi kèm yêu cầu đăng nhập (null = phiên ghi nhớ dùng chung). Tab đang có phiên riêng thì
    // dùng lại mã cũ để máy chủ thu hồi đúng phiên cũ của tab khi đăng nhập lại.
    function loginTabId(remember) { return remember ? null : (tabId() || newTabId()); }
    // Đăng nhập THÀNH CÔNG mới chuyển kiểu phiên của tab (thất bại thì tab giữ nguyên phiên cũ).
    function beginSession(id, token, user) {
        const oldTab = tabId();
        try {
            if (id) sessionStorage.setItem(TAB_KEY, id);
            else {
                sessionStorage.removeItem(TAB_KEY); sessionStorage.removeItem(TOKEN_KEY); sessionStorage.removeItem(USER_KEY);
                // Tab trước đó có phiên riêng mà nay đăng nhập ghi nhớ: thu hồi phiên riêng cũ của tab.
                if (oldTab) nativeFetch(API_BASE + '/auth/logout', { method: 'POST', credentials: 'include', headers: tabHeaders(oldTab) }).catch(function () { });
            }
        } catch { }
        setAccessToken(token);
        setUser(user);
    }

    async function refresh() {
        // Refresh token dùng 1 lần (rotation), nên nhiều request cùng lúc phải dùng chung 1 lần refresh
        if (!refreshing) {
            const before = accessToken;
            refreshing = (async () => {
                const res = await fetch(API_BASE + '/auth/refresh', {
                    method: 'POST', credentials: 'include', headers: tabHeaders()
                });
                const data = await res.json().catch(() => ({}));
                if (!res.ok) {
                    // Tab khác có thể vừa refresh xong (cookie chung đã xoay vòng) — dùng token nó để lại.
                    let shared = null;
                    try { shared = store().getItem(TOKEN_KEY); } catch { }
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

    // Đăng xuất phiên của tab này: tab có phiên riêng chỉ đăng xuất tab đó (các tab/tài khoản khác giữ nguyên);
    // phiên ghi nhớ dùng chung thì đăng xuất mọi tab đang dùng chung.
    async function logout(to) {
        try { await nativeFetch(API_BASE + '/auth/logout', { method: 'POST', credentials: 'include', headers: tabHeaders() }); } catch { }
        setAccessToken(null);
        setUser(null);
        try { sessionStorage.removeItem(TAB_KEY); } catch { }
        location.href = to || LOGIN_URL;
    }


    window.Auth = {
        API_BASE, setAccessToken, getAccessToken, setUser, getUser,
        refresh, apiFetch, requireAuth, tryGetMe, logout, loginTabId, beginSession, isTabSession: function () { return !!tabId(); }
    };
})();
