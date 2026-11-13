/* Trang Quản lý tài khoản (ADM-01/02). Cần auth.js (Auth.apiFetch, Auth.requireAuth) và Bootstrap bundle. */
(function () {
    'use strict';

    const API = '/api/admin/accounts';
    const PAGE_SIZE = 20;
    const REASON_PRESETS = [
        'Spam hoặc quảng cáo trái phép',
        'Ngôn từ xúc phạm, thù ghét',
        'Gian lận trong thi đấu',
        'Mạo danh người khác',
        'Vi phạm điều khoản sử dụng'
    ];
    const ERRORS = {
        REASON_INVALID: 'Lý do phải từ 5 đến 500 ký tự.',
        CANNOT_BAN_SELF: 'Bạn không thể tự khóa tài khoản của mình.',
        CANNOT_BAN_ADMIN: 'Không thể khóa tài khoản quản trị viên.',
        ALREADY_BANNED: 'Tài khoản này đã bị khóa. Danh sách sẽ được làm mới.',
        NOT_BANNED: 'Tài khoản này hiện không bị khóa. Danh sách sẽ được làm mới.',
        ACCOUNT_NOT_FOUND: 'Không tìm thấy tài khoản.'
    };

    const $ = (id) => document.getElementById(id);
    const state = { q: '', role: '', status: '', page: 1, total: 0, busy: false };
    let reqSeq = 0;          // bỏ qua phản hồi cũ khi gõ nhanh
    let modal = null;
    let current = null;      // { mode: 'ban' | 'unban', user }

    // ---------- tiện ích ----------
    function parseUtc(s) {
        if (!s) return null;
        return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(s) ? s : s + 'Z');
    }
    function fmtDate(s) {
        const d = parseUtc(s);
        return d ? d.toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' }) : '';
    }
    function el(tag, cls, text) {
        const e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }
    function msg(node, text, kind) {
        if (!text) { node.hidden = true; node.textContent = ''; return; }
        node.hidden = false;
        node.className = 'form-msg ' + (kind || 'error');
        node.textContent = text;
    }
    async function call(path, opts) {
        let res;
        try {
            res = await Auth.apiFetch(path, opts);
        } catch (e) {
            return { ok: false, status: 0, data: { message: 'Không kết nối được máy chủ.' } };
        }
        let data = {};
        try { data = await res.json(); } catch (_) { /* thân rỗng */ }
        return { ok: res.ok, status: res.status, data };
    }
    function errText(r) {
        if (r.status === 429) return 'Thao tác quá nhanh, vui lòng thử lại sau.';
        if (r.status === 403 && !r.data.code) return 'Bạn không có quyền thực hiện thao tác này.';
        return ERRORS[r.data.code] || r.data.message || 'Đã xảy ra lỗi, vui lòng thử lại.';
    }

    // ---------- danh sách ----------
    async function load() {
        const seq = ++reqSeq;
        $('rows').classList.add('is-loading');
        const p = new URLSearchParams({ page: state.page, pageSize: PAGE_SIZE });
        if (state.q) p.set('q', state.q);
        if (state.role) p.set('role', state.role);
        if (state.status) p.set('status', state.status);

        const r = await call(API + '?' + p.toString());
        if (seq !== reqSeq) return;
        $('rows').classList.remove('is-loading');

        if (!r.ok) {
            msg($('list-msg'), errText(r), 'error');
            return;
        }
        msg($('list-msg'), '');
        const d = r.data;
        state.total = d.total;

        // Nếu xóa/lọc làm trang hiện tại vượt quá số trang thì lùi về trang cuối.
        const pages = Math.max(1, Math.ceil(d.total / PAGE_SIZE));
        if (state.page > pages) { state.page = pages; return load(); }

        render(d);
    }

    function render(d) {
        const rows = $('rows');
        rows.textContent = '';
        d.items.forEach((u) => rows.appendChild(row(u)));
        $('empty').hidden = d.items.length > 0;

        document.querySelectorAll('[data-count]').forEach((n) => {
            n.textContent = d.counts[n.dataset.count] ?? 0;
        });

        const pages = Math.max(1, Math.ceil(d.total / PAGE_SIZE));
        const from = d.total === 0 ? 0 : (d.page - 1) * PAGE_SIZE + 1;
        const to = Math.min(d.total, d.page * PAGE_SIZE);
        $('range').textContent = d.total === 0 ? '' : `Hiển thị ${from}–${to} / ${d.total}`;
        $('page-label').textContent = `Trang ${d.page}/${pages}`;
        $('prev').disabled = d.page <= 1;
        $('next').disabled = d.page >= pages;
    }

    function statusBadge(status) {
        if (status === 'ACTIVE') return el('span', 'au-badge st-active', 'Hoạt động');
        if (status === 'BANNED') return el('span', 'au-badge st-banned', 'Bị khóa');
        return el('span', 'au-badge st-unverified', 'Chưa xác minh');
    }

    function row(u) {
        const tr = document.createElement('tr');

        // Người dùng
        const td1 = document.createElement('td');
        const box = el('div', 'au-user');
        let av;
        if (u.avatarUrl) {
            av = el('img', 'au-avatar');
            av.src = u.avatarUrl;
            av.alt = '';
            av.referrerPolicy = 'no-referrer';
            av.addEventListener('error', () => { av.replaceWith(initial(u)); });
        } else {
            av = initial(u);
        }
        const info = el('div');
        info.style.minWidth = '0';
        info.appendChild(el('div', 'au-name', u.fullName));
        info.appendChild(el('div', 'au-email', u.email));
        if (u.status === 'BANNED' && u.banReason) {
            const br = el('div', 'au-ban-reason', 'Lý do: ' + u.banReason);
            br.title = u.banReason;
            info.appendChild(br);
        }
        box.append(av, info);
        td1.appendChild(box);

        // Vai trò
        const td2 = document.createElement('td');
        td2.appendChild(el('span', 'au-badge role ' + u.role.toLowerCase(), u.role));

        // Trạng thái
        const td3 = document.createElement('td');
        td3.appendChild(statusBadge(u.status));

        // Ngày tạo
        const td4 = el('td', null, fmtDate(u.createdAt));

        // Thao tác
        const td5 = el('td', 'au-right');
        if (u.role === 'ADMIN') {
            const b = el('button', 'au-btn', 'Khóa');
            b.type = 'button';
            b.disabled = true;
            b.title = 'Không thể khóa quản trị viên';
            td5.appendChild(b);
        } else if (u.status === 'BANNED') {
            const b = el('button', 'au-btn au-btn-ok', 'Mở khóa');
            b.type = 'button';
            b.addEventListener('click', () => openDialog('unban', u));
            td5.appendChild(b);
        } else {
            const b = el('button', 'au-btn au-btn-danger', 'Khóa');
            b.type = 'button';
            b.addEventListener('click', () => openDialog('ban', u));
            td5.appendChild(b);
        }

        tr.append(td1, td2, td3, td4, td5);
        return tr;
    }

    function initial(u) {
        const ch = (u.fullName || u.email || '?').trim().charAt(0).toUpperCase();
        return el('div', 'au-avatar', ch);
    }

    // ---------- dialog ----------
    function openDialog(mode, user) {
        current = { mode, user };
        const isBan = mode === 'ban';
        $('modal-title').textContent = isBan ? 'Khóa tài khoản' : 'Mở khóa tài khoản';
        $('modal-target').textContent = `${user.fullName} · ${user.email}`;
        $('ban-fields').hidden = !isBan;
        $('unban-fields').hidden = isBan;
        const btn = $('modal-submit');
        btn.textContent = isBan ? 'Xác nhận khóa' : 'Xác nhận mở khóa';
        btn.className = 'au-btn ' + (isBan ? 'au-btn-danger' : 'au-btn-ok');
        btn.disabled = false;
        $('reason').value = '';
        $('note').value = '';
        $('reason-count').textContent = '0';
        msg($('modal-msg'), '');
        modal.show();
        setTimeout(() => (isBan ? $('reason') : $('note')).focus(), 250);
    }

    async function submitDialog(ev) {
        ev.preventDefault();
        if (!current || state.busy) return;
        const { mode, user } = current;
        const body = {};
        if (mode === 'ban') {
            const reason = $('reason').value.trim();
            if (reason.length < 5) {
                msg($('modal-msg'), 'Vui lòng nhập lý do vi phạm (tối thiểu 5 ký tự).', 'error');
                $('reason').focus();
                return;
            }
            body.reason = reason;
        } else {
            body.note = $('note').value.trim();
        }

        setBusy(true);
        const r = await call(`${API}/${user.accountId}/${mode}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        setBusy(false);

        if (r.ok) {
            modal.hide();
            msg($('list-msg'), (mode === 'ban' ? 'Đã khóa ' : 'Đã mở khóa ') + user.email + '.', 'success');
            load();
            return;
        }
        msg($('modal-msg'), errText(r), 'error');
        if (r.data.code === 'ALREADY_BANNED' || r.data.code === 'NOT_BANNED') load();
    }

    function setBusy(b) {
        state.busy = b;
        const btn = $('modal-submit');
        btn.disabled = b;
        $('reason').readOnly = b;
        $('note').readOnly = b;
        if (b) btn.dataset.label = btn.textContent, btn.textContent = 'Đang xử lý...';
        else if (btn.dataset.label) btn.textContent = btn.dataset.label;
    }

    // ---------- khởi tạo ----------
    function debounce(fn, ms) {
        let t;
        return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); };
    }

    async function init() {
        await Auth.requireAuth(['ADMIN']);

        modal = new bootstrap.Modal($('ban-modal'));

        REASON_PRESETS.forEach((text) => {
            const c = el('button', 'au-chip', text);
            c.type = 'button';
            c.addEventListener('click', () => {
                $('reason').value = text;
                $('reason-count').textContent = text.length;
                $('reason').focus();
            });
            $('chips').appendChild(c);
        });
        $('reason').addEventListener('input', () => {
            $('reason-count').textContent = $('reason').value.length;
        });
        $('ban-form').addEventListener('submit', submitDialog);

        $('filter-form').addEventListener('submit', (e) => e.preventDefault());
        $('q').addEventListener('input', debounce(() => {
            state.q = $('q').value.trim();
            state.page = 1;
            load();
        }, 350));
        $('role').addEventListener('change', () => {
            state.role = $('role').value;
            state.page = 1;
            load();
        });
        $('status-tabs').addEventListener('click', (e) => {
            const tab = e.target.closest('.au-tab');
            if (!tab) return;
            document.querySelectorAll('.au-tab').forEach((t) => t.classList.toggle('active', t === tab));
            state.status = tab.dataset.status;
            state.page = 1;
            load();
        });
        $('prev').addEventListener('click', () => { if (state.page > 1) { state.page--; load(); } });
        $('next').addEventListener('click', () => { state.page++; load(); });

        load();
    }

    document.addEventListener('DOMContentLoaded', init);
})();