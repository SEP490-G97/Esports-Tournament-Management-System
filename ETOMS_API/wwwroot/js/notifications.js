// Chuông thông báo trong app. Dùng: EtomsNotify.mount(slotElement) sau khi đã xác định người dùng đăng nhập.
(function () {
    'use strict';
    var POLL_MS = 120000, PAGE = 10;   // polling chỉ là lưới an toàn; thông báo mới đến qua luồng real-time (SSE)
    var mounted = false;

    function esc(s) { return window.Ui ? Ui.escapeHtml(s) : String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return '&#' + c.charCodeAt(0) + ';'; }); }

    function ensureCss() {
        if (document.getElementById('etn-css')) return;
        var l = document.createElement('link');
        l.id = 'etn-css'; l.rel = 'stylesheet'; l.href = '/css/notifications.css';
        document.head.appendChild(l);
    }

    function when(iso) {
        if (!iso) return '';
        var d = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : iso + 'Z');
        var s = Math.floor((Date.now() - d.getTime()) / 1000);
        if (s < 60) return 'Vừa xong';
        if (s < 3600) return Math.floor(s / 60) + ' phút trước';
        if (s < 86400) return Math.floor(s / 3600) + ' giờ trước';
        if (s < 7 * 86400) return Math.floor(s / 86400) + ' ngày trước';
        return d.toLocaleDateString('vi-VN');
    }

    async function api(path, method) {
        var res = await Auth.apiFetch(path, { method: method || 'GET' });
        if (!res || !res.ok) return null;
        if (res.status === 204) return {};
        try { return await res.json(); } catch (e) { return {}; }
    }

    function mount(slot) {
        if (mounted || !slot || !window.Auth || !Auth.apiFetch) return;
        mounted = true;
        ensureCss();

        var wrap = document.createElement('div');
        wrap.className = 'etn-bell';
        wrap.innerHTML =
            '<button type="button" class="etn-btn" aria-label="Thông báo" aria-haspopup="true" aria-expanded="false">' +
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/></svg>' +
            '<span class="etn-badge" hidden>0</span></button>' +
            '<div class="etn-panel" role="dialog" aria-label="Thông báo" hidden>' +
            '<div class="etn-head"><span>Thông báo</span><button type="button" class="etn-link" data-all>Đánh dấu đã đọc tất cả</button></div>' +
            '<div class="etn-list"></div>' +
            '<div class="etn-foot" hidden><button type="button" class="etn-link" data-more>Xem thêm</button></div></div>';
        slot.insertBefore(wrap, slot.firstChild);

        var btn = wrap.querySelector('.etn-btn'), badge = wrap.querySelector('.etn-badge'),
            panel = wrap.querySelector('.etn-panel'), list = wrap.querySelector('.etn-list'),
            foot = wrap.querySelector('.etn-foot'), allBtn = wrap.querySelector('[data-all]'),
            moreBtn = wrap.querySelector('[data-more]');
        var page = 1, unread = 0, loading = false;

        function setUnread(n) {
            unread = Math.max(0, n | 0);
            badge.hidden = unread === 0;
            badge.textContent = unread > 99 ? '99+' : String(unread);
            btn.setAttribute('aria-label', unread ? 'Thông báo (' + unread + ' chưa đọc)' : 'Thông báo');
            allBtn.disabled = unread === 0;
        }
        function state(text) { list.innerHTML = '<div class="etn-state">' + esc(text) + '</div>'; }

        function row(n) {
            var b = document.createElement('button');
            b.type = 'button';
            b.className = 'etn-item' + (n.isRead ? '' : ' unread');
            b.setAttribute('data-id', n.id);
            b.innerHTML = '<div class="t">' + (n.isRead ? '' : '<i></i>') + '<span>' + esc(n.title) + '</span></div>' +
                '<div class="b">' + esc(n.body) + '</div><div class="d">' + esc(when(n.sentAt)) + '</div>';
            b.addEventListener('click', async function () {
                if (!n.isRead) {
                    n.isRead = true; b.classList.remove('unread');
                    var dot = b.querySelector('.t i'); if (dot) dot.remove();
                    setUnread(unread - 1);
                    await api('/api/notifications/' + n.id + '/read', 'POST');
                }
                var href = window.Ui ? Ui.safeUrl(n.linkUrl) : '';
                if (href) location.href = href;
            });
            return b;
        }

        async function load(reset) {
            if (loading) return;
            loading = true;
            if (reset) { page = 1; state('Đang tải…'); }
            var d = await api('/api/notifications?page=' + page + '&pageSize=' + PAGE);
            loading = false;
            if (!d) { if (reset) state('Không tải được thông báo.'); return; }
            if (reset) list.innerHTML = '';
            if (d.items.length === 0 && reset) state('Bạn chưa có thông báo nào.');
            d.items.forEach(function (n) { list.appendChild(row(n)); });
            setUnread(d.unread);
            foot.hidden = page * d.pageSize >= d.total;
        }

        function open(on) {
            panel.hidden = !on;
            btn.setAttribute('aria-expanded', String(on));
            if (on) load(true);
        }
        btn.addEventListener('click', function (e) { e.stopPropagation(); open(panel.hidden); });
        document.addEventListener('click', function (e) { if (!wrap.contains(e.target)) open(false); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !panel.hidden) { open(false); btn.focus(); } });
        moreBtn.addEventListener('click', function () { page++; load(false); });
        allBtn.addEventListener('click', async function () {
            allBtn.disabled = true;
            if (await api('/api/notifications/read-all', 'POST')) {
                setUnread(0);
                list.querySelectorAll('.etn-item.unread').forEach(function (x) { x.classList.remove('unread'); var i = x.querySelector('.t i'); if (i) i.remove(); });
            } else allBtn.disabled = false;
        });

        // ---- real-time -------------------------------------------------------------------
        function toast(n) {
            var host = document.getElementById('etn-toasts');
            if (!host) { host = document.createElement('div'); host.id = 'etn-toasts'; host.className = 'etn-toasts'; host.setAttribute('aria-live', 'polite'); document.body.appendChild(host); }
            while (host.children.length >= 3) host.removeChild(host.firstChild);
            var t = document.createElement('button');
            t.type = 'button'; t.className = 'etn-toast';
            t.innerHTML = '<b>' + esc(n.title) + '</b><span>' + esc(n.body) + '</span>';
            t.addEventListener('click', function (e) { e.stopPropagation(); t.remove(); open(true); });
            host.appendChild(t);
            setTimeout(function () { t.remove(); }, 7000);
        }
        function onPush(n) {
            if (!n || typeof n.id !== 'number') return;
            if (panel.hidden || !list.querySelector('[data-id="' + n.id + '"]')) setUnread(unread + 1);
            if (!panel.hidden && !list.querySelector('[data-id="' + n.id + '"]')) {
                var empty = list.querySelector('.etn-state'); if (empty) empty.remove();
                list.insertBefore(row(n), list.firstChild);
            }
            toast(n);
        }

        var backoff = 2000, stopped = false;
        async function connect() {
            if (stopped) return;
            var gotData = false;
            try {
                var res = await Auth.apiFetch('/api/notifications/stream', { headers: { 'Accept': 'text/event-stream' } });
                if (!res || !res.ok || !res.body) throw new Error('stream ' + (res && res.status));
                var reader = res.body.getReader(), dec = new TextDecoder(), buf = '';
                backoff = 2000;
                poll();                                   // bù thông báo đến trong lúc mất kết nối
                for (; ;) {
                    var chunk = await reader.read();
                    if (chunk.done) break;
                    gotData = true;
                    buf += dec.decode(chunk.value, { stream: true }).replace(/\r\n/g, '\n');
                    var i;
                    while ((i = buf.indexOf('\n\n')) >= 0) {
                        var frame = buf.slice(0, i); buf = buf.slice(i + 2);
                        var ev = 'message', data = '';
                        frame.split('\n').forEach(function (ln) {
                            if (ln.indexOf('event:') === 0) ev = ln.slice(6).trim();
                            else if (ln.indexOf('data:') === 0) data += ln.slice(5).trim();
                        });
                        if (ev === 'notification' && data) { try { onPush(JSON.parse(data)); } catch (e) { /* bỏ qua khung lỗi */ } }
                    }
                }
            } catch (e) { /* mất mạng / server khởi động lại: thử nối lại bên dưới */ }
            if (stopped) return;
            // Server chủ động đóng sau ~10 phút (đổi token) → nối lại ngay; lỗi thật → lùi dần tới 60 giây.
            var wait = gotData ? 500 : backoff;
            if (!gotData) backoff = Math.min(backoff * 2, 60000);
            setTimeout(connect, wait);
        }
        window.addEventListener('beforeunload', function () { stopped = true; });
        connect();

        async function poll() {
            if (document.hidden) return;
            var d = await api('/api/notifications/unread-count');
            if (d && typeof d.unread === 'number') setUnread(d.unread);
        }
        poll();
        setInterval(poll, POLL_MS);
        document.addEventListener('visibilitychange', function () { if (!document.hidden) poll(); });
    }

    window.EtomsNotify = { mount: mount };
})();