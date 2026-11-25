/* Trang Quản lý tài khoản (ADM-01/02). Cần auth.js (Auth.apiFetch, Auth.requireAuth) và Bootstrap bundle. */
(function () {
    'use strict';

    const API = '/api/admin/accounts';
    // Số dòng mỗi trang chọn được (10/20/50/100), nhớ trong trình duyệt.
    let pageSize = 20;
    try { const saved = parseInt(localStorage.getItem('au-page-size'), 10); if ([10, 20, 50, 100].includes(saved)) pageSize = saved; } catch (_) { /* bỏ qua */ }
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
    const state = { q: '', role: '', status: '', sort: 'newest', page: 1, total: 0, busy: false };
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
        const p = new URLSearchParams({ page: state.page, pageSize: pageSize, sort: state.sort });
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
        const pages = Math.max(1, Math.ceil(d.total / pageSize));
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

        const pages = Math.max(1, Math.ceil(d.total / pageSize));
        const from = d.total === 0 ? 0 : (d.page - 1) * pageSize + 1;
        const to = Math.min(d.total, d.page * pageSize);
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
        td2.appendChild(el('span', 'au-badge role ' + u.role.toLowerCase(), ({ PLAYER: 'Người chơi', ORGANIZER: 'Ban tổ chức', ADMIN: 'Quản trị viên' })[u.role] || u.role));

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
        $('chair-impact').hidden = true;
        $('chair-impact').textContent = '';
        $('modal-remind').hidden = true;
        modal.show();
        setTimeout(() => (isBan ? $('reason') : $('note')).focus(), 250);
        if (isBan && user.role === 'ORGANIZER') loadImpact(user);
    }

    // Khóa một Ban tổ chức đang là CHỦ NHIỆM đơn vị: báo trước hậu quả và cho Admin chọn
    //  (1) bàn giao chủ nhiệm cho một thành viên rồi khóa, hoặc
    //  (2) chưa khóa — gửi thông báo yêu cầu người đó tự bàn giao (nút "Gửi yêu cầu tự bàn giao").
    async function loadImpact(user) {
        const box = $('chair-impact');
        const r = await call(`${API}/${user.accountId}/ban-impact`);
        if (!current || current.user !== user) return;           // đã đóng / mở người khác
        current.impact = r.ok ? r.data : null;
        const orgs = (r.ok && r.data.chairedOrganizations) || [];
        if (!orgs.length) return;

        box.textContent = '';
        const head = el('p');
        head.append('Tài khoản này đang là ', el('b', null, 'chủ nhiệm ' + orgs.length + ' đơn vị tổ chức'),
            '. Nếu khóa, chủ nhiệm không đăng nhập được nên đơn vị sẽ không còn ai quản lý thành viên, hồ sơ và giải đấu' +
            (r.data.openTournamentCount ? ` (đang có ${r.data.openTournamentCount} giải chưa kết thúc do người này quản lý)` : '') + '.');
        box.appendChild(head);
        box.appendChild(el('p', null, 'Chọn người nhận bàn giao chủ nhiệm cho từng đơn vị rồi bấm "Xác nhận khóa" — giải chưa kết thúc của đơn vị sẽ chuyển sang chủ nhiệm mới. Hoặc bấm "Gửi yêu cầu tự bàn giao" để báo người này tự bàn giao trước (chưa khóa).'));

        orgs.forEach((o) => {
            const wrap = el('div', 'au-impact-org');
            const id = 'handover-' + o.organizationId;
            const label = el('label', 'au-label', o.name + (o.openTournamentCount ? ` · ${o.openTournamentCount} giải chưa kết thúc` : ''));
            label.htmlFor = id;
            const sel = el('select', 'au-input');
            sel.id = id;
            sel.dataset.org = o.organizationId;
            o.candidates.forEach((c) => {
                const opt = el('option', null, `Bàn giao cho ${c.fullName} (${c.orgRoleLabel}) · ${c.email}`);
                opt.value = c.accountId;
                sel.appendChild(opt);
            });
            const none = el('option', null, o.candidates.length
                ? 'Không bàn giao — đơn vị tạm không có chủ nhiệm'
                : 'Đơn vị chưa có thành viên Ban tổ chức nào khác — khóa mà không bàn giao');
            none.value = '';
            sel.appendChild(none);
            wrap.append(label, sel);
            box.appendChild(wrap);
        });
        box.hidden = false;
        $('modal-remind').hidden = false;
    }

    async function sendHandoverReminder() {
        if (!current || state.busy) return;
        const reason = $('reason').value.trim();
        if (reason.length < 5) {
            msg($('modal-msg'), 'Vui lòng nhập lý do (tối thiểu 5 ký tự) — lý do được gửi kèm yêu cầu bàn giao.', 'error');
            $('reason').focus();
            return;
        }
        const user = current.user;
        setBusy(true);
        const r = await call(`${API}/${user.accountId}/request-handover`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ reason })
        });
        setBusy(false);
        if (r.ok) {
            modal.hide();
            msg($('list-msg'), `Đã gửi yêu cầu tự bàn giao chủ nhiệm cho ${user.email}. Tài khoản chưa bị khóa.`, 'success');
            return;
        }
        msg($('modal-msg'), errText(r), 'error');
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
            const selects = document.querySelectorAll('#chair-impact select[data-org]');
            if (selects.length) {
                body.handovers = [];
                selects.forEach((s) => {
                    if (s.value) body.handovers.push({ organizationId: Number(s.dataset.org), newChairAccountId: Number(s.value) });
                    else body.confirmWithoutHandover = true;   // Admin chọn rõ "không bàn giao" sau khi đã thấy cảnh báo
                });
            }
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
        // Admin bấm khóa trước khi phần cảnh báo chủ nhiệm kịp hiện: hiện lại để Admin chọn cách xử lý.
        if (r.data.code === 'CHAIR_HANDOVER_REQUIRED' && current) loadImpact(current.user);
    }

    function setBusy(b) {
        state.busy = b;
        const btn = $('modal-submit');
        btn.disabled = b;
        $('modal-remind').disabled = b;
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
        // Không phải Admin: requireAuth đã chuyển trang đi — dừng tại đây, không gọi API quản trị.
        if (!(await Auth.requireAuth(['ADMIN']))) return;

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
        $('modal-remind').addEventListener('click', sendHandoverReminder);

        $('filter-form').addEventListener('submit', (e) => e.preventDefault());
        $('q').addEventListener('input', debounce(() => {
            state.q = $('q').value.trim();
            state.page = 1;
            load();
        }, 350));
        // Bấm tiêu đề cột để sắp xếp (rê chuột có gợi ý); bấm lần nữa đảo chiều.
        function paintSort() {
            document.querySelectorAll('.au-sort').forEach((b) => {
                const ic = b.querySelector('.au-sort-ic');
                const on = b.dataset.sort === 'name' ? state.sort.startsWith('name') : (state.sort === 'newest' || state.sort === 'oldest');
                b.classList.toggle('active', on);
                b.setAttribute('aria-sort', !on ? 'none' : (state.sort === 'name' || state.sort === 'oldest') ? 'ascending' : 'descending');
                ic.textContent = !on ? '↕' : (state.sort === 'name' ? '↑' : state.sort === 'name_desc' ? '↓' : state.sort === 'oldest' ? '↑' : '↓');
            });
        }
        document.querySelectorAll('.au-sort').forEach((b) => b.addEventListener('click', () => {
            state.sort = b.dataset.sort === 'name'
                ? (state.sort === 'name' ? 'name_desc' : 'name')
                : (state.sort === 'newest' ? 'oldest' : 'newest');
            state.page = 1;
            paintSort();
            load();
        }));
        paintSort();
        $('page-size').value = String(pageSize);
        $('page-size').addEventListener('change', () => {
            pageSize = parseInt($('page-size').value, 10) || 20;
            try { localStorage.setItem('au-page-size', String(pageSize)); } catch (_) { /* bỏ qua */ }
            state.page = 1;
            load();
        });
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