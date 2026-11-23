// Tiện ích dùng chung cho mọi trang tĩnh: thoát HTML, hiện alert/toast, gán lỗi theo field.
var Ui = (function () {
    function escapeHtml(s) {
        if (s === null || s === undefined) return '';
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function safeUrl(value) {
        if (!value) return '';
        try {
            var url = new URL(value, location.origin);
            return url.protocol === 'http:' || url.protocol === 'https:' ? url.href : '';
        } catch (e) {
            return '';
        }
    }

    // containerEl: phần tử chứa alert (thường là 1 <div id="flash"></div> đầu trang/khối).
    function showAlert(containerEl, type, message) {
        if (!containerEl) return;
        containerEl.innerHTML = '<div class="alert alert-' + (type === 'success' ? 'success' : 'danger') + '">' + escapeHtml(message) + '</div>';
    }

    function clearAlert(containerEl) {
        if (containerEl) containerEl.innerHTML = '';
    }

    // form: phần tử <form>/<div> chứa các span lỗi field, mỗi span có data-field-error="TenField".
    function clearFieldErrors(form) {
        if (!form) return;
        form.querySelectorAll('[data-field-error]').forEach(function (el) { el.textContent = ''; });
    }

    function applyFieldErrors(form, fieldErrors) {
        if (!form || !fieldErrors) return;
        Object.keys(fieldErrors).forEach(function (key) {
            var el = form.querySelector('[data-field-error="' + key + '"]');
            if (!el) return;
            var v = fieldErrors[key];
            el.textContent = Array.isArray(v) ? v[0] : v;
        });
    }

    function qs(name) {
        return new URLSearchParams(location.search).get(name);
    }

    function qsInt(name) {
        var v = qs(name);
        var n = v !== null ? parseInt(v, 10) : NaN;
        return isNaN(n) ? null : n;
    }

    // Người dùng hiện tại (đăng nhập thật, JWT) hoặc null nếu là khách. Không chuyển trang.
    // Kết quả có: isAuthenticated, userId, accountId, displayName, fullName, role.
    // Gọi nhiều lần trong 1 trang (nav + trang) chỉ hỏi máy chủ 1 lần.
    var mePromise = null;
    function currentUser() {
        if (!mePromise) {
            mePromise = (async function () {
                if (!(window.Auth && typeof Auth.tryGetMe === 'function')) return null;
                var me = await Auth.tryGetMe();
                if (!me) return null;
                me.isAuthenticated = true;
                me.userId = me.accountId = me.id;
                me.displayName = me.fullName = me.name;
                return me;
            })();
        }
        return mePromise.then(function (me) { return me ? Object.assign({}, me) : null; });
    }

    var ROLE_LABELS = { PLAYER: 'tuyển thủ', ORGANIZER: 'Ban tổ chức', ADMIN: 'Quản trị viên' };

    // Sai vai trò: báo ngay tại trang (trước đây âm thầm chuyển về trang chủ, người dùng tưởng lỗi).
    function showRoleNotice(roles, me) {
        var main = document.querySelector('main');
        if (!main) return;
        var allowed = roles.map(function (r) { return ROLE_LABELS[r] || r; }).join(' hoặc ');
        main.innerHTML = '<div class="card-etoms" style="margin-top:24px"><div class="empty">' +
            '<h3>Trang này dành cho tài khoản ' + escapeHtml(allowed) + '</h3>' +
            '<p>Bạn đang đăng nhập với vai trò ' + escapeHtml(ROLE_LABELS[me.role] || me.role) +
            ' nên không dùng được chức năng này.</p>' +
            '<a class="btn btn-primary mt-3" href="/">Về trang chủ</a></div></div>';
    }

    // Bắt buộc đăng nhập: khách thì chuyển sang trang đăng nhập kèm returnUrl và trả về null.
    // roles (tùy chọn): chỉ cho phép các vai trò này; sai vai trò thì hiện thông báo tại trang, trả về null.
    async function requireAuth(roles) {
        var me = await currentUser();
        if (!me) {
            var returnUrl = encodeURIComponent(location.pathname + location.search);
            location.href = '/auth/login.html?returnUrl=' + returnUrl;
            return null;
        }
        if (roles && roles.length && roles.indexOf(me.role) < 0) { showRoleNotice(roles, me); return null; }
        return me;
    }

    // Flash message xuyên trang (thay TempData["ThanhCong"]/["LoiChung"] cũ): đặt trước khi
    // điều hướng sang trang khác (vd sau khi Disband), đọc 1 lần rồi tự xoá ở trang đích.
    function setFlash(message, type) {
        try { sessionStorage.setItem('etoms-flash', JSON.stringify({ message: message, type: type || 'success' })); } catch (e) {}
    }

    function consumeFlash(containerEl) {
        try {
            var raw = sessionStorage.getItem('etoms-flash');
            if (!raw) return;
            sessionStorage.removeItem('etoms-flash');
            var f = JSON.parse(raw);
            showAlert(containerEl, f.type, f.message);
        } catch (e) {}
    }

    // Máy chủ trả giờ UTC KHÔNG có hậu tố Z (cột DATETIME của MySQL). new Date(chuỗi) sẽ hiểu nhầm thành giờ máy
    // người xem (lệch 7 tiếng ở VN) — mọi trang hiển thị ngày giờ nên đi qua hai hàm này.
    function parseUtc(v) {
        if (!v) return null;
        if (v instanceof Date) return v;
        var s = String(v);
        var hasZone = /Z$|[+-]\d\d:?\d\d$/.test(s);
        var hasTime = /T\d|\d:\d/.test(s);
        return new Date(hasZone || !hasTime ? s : s + 'Z');
    }
    function fmtDateTime(v, empty) {
        var d = parseUtc(v);
        if (!d || isNaN(d.getTime())) return empty === undefined ? '' : empty;
        return d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    // Danh sách dài (giải của đơn vị, lịch sử đội...): chỉ hiện `step` mục đầu, nút "Xem thêm" mở tiếp từng đợt.
    // Dùng style.display chứ không dùng thuộc tính hidden vì CSS display:flex của thẻ con sẽ đè hidden.
    function limitList(listEl, step) {
        if (!listEl) return;
        step = step || 10;
        var items = Array.prototype.slice.call(listEl.children);
        if (items.length <= step) return;
        var shown = step;
        var wrap = document.createElement('div');
        wrap.className = 'btn-row justify-content-center mt-2';
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn btn-ghost btn-sm';
        wrap.appendChild(btn);
        listEl.insertAdjacentElement('afterend', wrap);
        function apply() {
            items.forEach(function (el, i) { el.style.display = i < shown ? '' : 'none'; });
            btn.textContent = 'Xem thêm (còn ' + (items.length - shown) + ')';
            wrap.style.display = shown >= items.length ? 'none' : '';
        }
        btn.addEventListener('click', function () { shown += step; apply(); });
        apply();
    }

    // Thanh số trang dùng chung: ‹ Trước 1 … 4 5 6 … 20 Sau ›. Nút mang data-page để trang tự bắt sự kiện.
    function pagerHtml(page, totalPages) {
        if (totalPages <= 1) return '';
        function btn(p, label, disabled, active) {
            return '<button type="button" class="btn ' + (active ? 'btn-primary' : 'btn-ghost') + ' btn-sm" data-page="' + p + '"' +
                (disabled ? ' disabled' : '') + (active ? ' aria-current="page"' : '') + '>' + label + '</button>';
        }
        var html = btn(page - 1, '‹ Trước', page <= 1, false);
        for (var p = 1; p <= totalPages; p++) {
            if (p === 1 || p === totalPages || Math.abs(p - page) <= 1) html += btn(p, String(p), false, p === page);
            else if (Math.abs(p - page) === 2) html += '<span class="hint px-1">…</span>';
        }
        html += btn(page + 1, 'Sau ›', page >= totalPages, false);
        return '<nav class="pager" aria-label="Phân trang">' + html + '</nav>';
    }

    // Danh sách dài phía trình duyệt: ô tìm kiếm + phân trang số. Dùng cho danh sách đã tải hết một lần
    // (giải của đơn vị, lịch sử đội, lịch sử trận...). opts: items, render(item) -> html, text(item) -> chuỗi để tìm,
    // pageSize (mặc định 10), placeholder, emptyText, listClass (mặc định feature-list),
    // wrap(rowsHtml) -> html (bảng: bọc <table>), after(bodyEl) gọi sau mỗi lần vẽ để gắn sự kiện.
    function listPager(root, opts) {
        if (!root) return;
        var items = opts.items || [];
        var size = opts.pageSize || 10;
        var page = 1, keyword = '';
        if (!items.length) { root.innerHTML = '<p class="muted-copy mt-3">' + escapeHtml(opts.emptyText || 'Chưa có dữ liệu.') + '</p>'; return; }
        var searchable = items.length > size && typeof opts.text === 'function';
        root.innerHTML = (searchable ? '<div class="list-search"><input type="search" class="input" maxlength="100" placeholder="' +
            escapeHtml(opts.placeholder || 'Tìm trong danh sách...') + '" aria-label="' + escapeHtml(opts.placeholder || 'Tìm trong danh sách') + '" /></div>' : '') +
            '<div class="' + (opts.listClass || 'feature-list') + ' mt-3" data-list-body></div><div data-list-pager></div>';
        var body = root.querySelector('[data-list-body]');
        var pager = root.querySelector('[data-list-pager]');
        // Bỏ dấu tiếng Việt để gõ "giai mua he" vẫn tìm ra "Giải mùa hè": tách dấu (NFD) rồi bỏ các ký tự dấu U+0300..U+036F.
        function norm(s) {
            var t = String(s || '').normalize('NFD'), out = '';
            for (var i = 0; i < t.length; i++) {
                var c = t.charCodeAt(i);
                if (c < 0x300 || c > 0x36f) out += t.charAt(i);
            }
            return out.replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
        }
        function draw() {
            var k = norm(keyword.trim());
            var list = k ? items.filter(function (it) { return norm(opts.text(it)).indexOf(k) >= 0; }) : items;
            var pages = Math.max(1, Math.ceil(list.length / size));
            if (page > pages) page = pages;
            var rows = list.slice((page - 1) * size, page * size).map(opts.render).join('');
            body.innerHTML = list.length ? (opts.wrap ? opts.wrap(rows) : rows)
                : '<p class="muted-copy">Không có mục nào khớp "' + escapeHtml(keyword.trim()) + '".</p>';
            pager.innerHTML = (list.length > size ? '<div class="hint mt-2">' + list.length + ' mục · trang ' + page + '/' + pages + '</div>' : '') + pagerHtml(page, pages);
            if (opts.after) opts.after(body);   // gắn lại sự kiện cho các nút trong trang vừa vẽ
        }
        pager.addEventListener('click', function (e) {
            var b = e.target.closest('[data-page]');
            if (!b || b.disabled) return;
            page = parseInt(b.dataset.page, 10); draw();
            root.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
        var input = root.querySelector('.list-search input');
        if (input) input.addEventListener('input', function () { keyword = input.value; page = 1; draw(); });
        draw();
    }

    return {
        parseUtc: parseUtc, fmtDateTime: fmtDateTime, limitList: limitList, pagerHtml: pagerHtml, listPager: listPager,
        escapeHtml: escapeHtml, safeUrl: safeUrl, showAlert: showAlert, clearAlert: clearAlert, clearFieldErrors: clearFieldErrors,
        applyFieldErrors: applyFieldErrors, qs: qs, qsInt: qsInt, requireAuth: requireAuth, currentUser: currentUser,
        setFlash: setFlash, consumeFlash: consumeFlash
    };
})();
