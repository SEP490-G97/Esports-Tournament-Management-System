(function () {
    const nativeFetch = window.fetch.bind(window);
    const API_BASE = '';                     
    const LOGIN_URL = '/auth/login.html';   

    let accessToken = null;
    let refreshing = null;                  // gộp nhiều lần refresh đồng thời thành 1

    function setAccessToken(t) { accessToken = t; }
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
                accessToken = data.accessToken;
                return accessToken;
            })().finally(() => { refreshing = null; });
        }
        return refreshing;
    }

    function redirectToLogin(banData) {
        accessToken = null;
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
    async function tryGetMe() {
        try { if (!accessToken) await refresh(); } catch (e) { return null; }
        const res = await nativeFetch(API_BASE + '/api/me', {
            credentials: 'include',
            headers: { Authorization: 'Bearer ' + accessToken },
        });
        if (!res.ok) return null;
        const me = await res.json();
        setUser(me);
        return me;
    }

    async function logout(to) {
        try { await nativeFetch(API_BASE + '/auth/logout', { method: 'POST', credentials: 'include' }); } catch { }
        accessToken = null;
        try { sessionStorage.removeItem('user'); } catch { }
        location.href = to || LOGIN_URL;
    }


    window.Auth = {
        API_BASE, setAccessToken, getAccessToken, setUser, getUser,
        refresh, apiFetch, requireAuth, tryGetMe, logout
    };
})();