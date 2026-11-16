/* ADM-08 – Thông báo toàn hệ thống & ghim Banner tổng */
(async function () {
    'use strict';

    const U = window.AdminUI;
    const el = U.el;
    const $ = (id) => document.getElementById(id);

    await Auth.requireAuth(['ADMIN']);
    U.renderNav('announcements');

    const CHANNELS = { IN_APP: 'Trong ứng dụng', EMAIL: 'Email', BOTH: 'Ứng dụng + Email' };
    const ROLE_NAMES = { PLAYER: 'Người chơi', ORGANIZER: 'Ban tổ chức', ADMIN: 'Quản trị viên' };
    const TYPES = { EVENT: 'Sự kiện', GIVEAWAY: 'Giveaway' };
    const STATES = {
        LIVE: ['Đang hiển thị', 'ok'], SCHEDULED: ['Đã lên lịch', 'info'],
        INACTIVE: ['Đang tắt', 'off'], EXPIRED: ['Hết hạn', 'warn']
    };

    // ------------------------------------------------------------------ tab chính
    document.querySelectorAll('#mainTabs .ax-tab').forEach(function (t) {
        t.addEventListener('click', function () {
            document.querySelectorAll('#mainTabs .ax-tab').forEach(function (x) { x.setAttribute('aria-selected', String(x === t)); });
            $('panel-announce').hidden = t.dataset.tab !== 'announce';
            $('panel-banner').hidden = t.dataset.tab !== 'banner';
            if (t.dataset.tab === 'banner' && !bannerLoaded) loadBanners();
        });
    });

    function showErr(node, msg) { node.textContent = msg || ''; node.hidden = !msg; }
    function skeleton(host, n) {
        U.clear(host);
        for (let i = 0; i < n; i++) {
            host.appendChild(el('div', { class: 'ax-item-skel' },
                el('div', { class: 'ax-skel', attrs: { style: 'width:28px;height:28px;border-radius:50%' } }),
                el('div', { class: 'ax-item-main' },
                    el('div', { class: 'ax-skel', attrs: { style: 'height:14px;width:45%;margin-bottom:8px' } }),
                    el('div', { class: 'ax-skel', attrs: { style: 'height:11px;width:70%' } }))));
        }
    }

    // ================================================================== THÔNG BÁO
    const aTitle = $('aTitle'), aBody = $('aBody'), aChannel = $('aChannel'), aLink = $('aLink');
    const aSend = $('aSend'), aErr = $('aErr'), aAudience = $('aAudience');
    let sending = false;

    function selectedRoles() {
        return Array.from(document.querySelectorAll('#aRoles input:checked')).map(function (i) { return i.value; });
    }
    function counters() {
        $('aTitleN').textContent = aTitle.value.length + '/200';
        $('aBodyN').textContent = aBody.value.length + '/1000';
    }
    aTitle.addEventListener('input', counters);
    aBody.addEventListener('input', counters);

    // ---- đối tượng: theo vai trò / trừ một số email / chỉ email chỉ định
    const aMode = $('aMode'), aEmails = $('aEmails');
    const EMAIL_RE = /^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$/;

    function parseEmails() {
        const seen = new Set(), list = [], bad = [];
        aEmails.value.split(/[\s,;]+/).forEach(function (raw) {
            const e = raw.trim().toLowerCase();
            if (!e) return;
            if (!EMAIL_RE.test(e)) { bad.push(e); return; }
            if (!seen.has(e)) { seen.add(e); list.push(e); }
        });
        return { list: list, bad: bad };
    }
    function emailsCounter() {
        const p = parseEmails();
        $('aEmailsN').textContent = p.list.length + ' email' + (p.bad.length ? ' · ' + p.bad.length + ' không hợp lệ' : '');
    }
    function applyMode() {
        const m = aMode.value;
        $('aRolesBox').hidden = m === 'ONLY';
        $('aEmailsBox').hidden = m === 'ROLES';
        $('aEmailsLabel').textContent = m === 'ONLY' ? 'Gửi cho các email' : 'Loại trừ các email';
        $('aEmailsHelp').textContent = m === 'ONLY'
            ? 'Bỏ qua vai trò và cài đặt nhận thông báo của người nhận. Email chưa có tài khoản vẫn nhận được nếu chọn kênh Email. Tối đa 200 email.'
            : 'Các tài khoản có email này sẽ không nhận thông báo. Tối đa 1000 email.';
        emailsCounter();
        refreshAudienceSoon();
    }
    function buildRequest() {
        const m = aMode.value, emails = parseEmails().list;
        const req = { channel: aChannel.value };
        if (m === 'ONLY') req.emails = emails;
        else { req.roles = selectedRoles(); if (m === 'EXCEPT') req.excludeEmails = emails; }
        return req;
    }
    /** Trả thông báo lỗi nhập liệu của phần đối tượng, hoặc '' nếu ổn. */
    function audienceProblem() {
        const m = aMode.value;
        if (m !== 'ONLY' && selectedRoles().length === 0) return 'Hãy chọn ít nhất một nhóm người nhận.';
        if (m !== 'ROLES') {
            const p = parseEmails();
            if (p.bad.length) return 'Email không hợp lệ: ' + p.bad.slice(0, 3).join(', ') + (p.bad.length > 3 ? '…' : '');
            if (m === 'ONLY' && p.list.length === 0) return 'Hãy nhập ít nhất một email.';
            if (m === 'ONLY' && p.list.length > 200) return 'Tối đa 200 email chỉ định.';
            if (m === 'EXCEPT' && p.list.length > 1000) return 'Tối đa 1000 email loại trừ.';
        }
        return '';
    }

    let audSeq = 0;
    let audience = null;
    async function refreshAudience() {
        audience = null;
        const problem = audienceProblem();
        if (problem) { ++audSeq; aAudience.className = 'ax-audience zero'; aAudience.textContent = problem; return; }
        const seq = ++audSeq;
        aAudience.className = 'ax-audience';
        U.clear(aAudience);
        aAudience.appendChild(el('span', { class: 'ax-spin' }));
        aAudience.appendChild(document.createTextNode('Đang đếm người nhận…'));
        const res = await U.api('/api/admin/announcements/audience', { method: 'POST', body: buildRequest() });
        if (seq !== audSeq) return; // có yêu cầu mới hơn
        U.clear(aAudience);
        if (!res.ok) { aAudience.className = 'ax-audience zero'; aAudience.textContent = U.errMsg(res, 'Không đếm được người nhận.'); return; }
        audience = res.data;
        aAudience.className = 'ax-audience' + (audience.recipients === 0 ? ' zero' : '');
        aAudience.appendChild(document.createTextNode('Sẽ gửi tới '));
        aAudience.appendChild(el('b', { text: U.fmtNumber(audience.recipients) }));
        let extra = ' người (' + U.fmtNumber(audience.accounts) + ' tài khoản; ' +
            U.fmtNumber(audience.inApp) + ' nhận trong ứng dụng, ' + U.fmtNumber(audience.email) + ' nhận email)';
        if (audience.excluded > 0) extra += ' · loại trừ ' + U.fmtNumber(audience.excluded) + ' tài khoản';
        if (audience.externalEmails > 0) extra += ' · ' + U.fmtNumber(audience.externalEmails) + ' email chưa có tài khoản (chỉ nhận mail)';
        if (audience.unreachable > 0) extra += ' · ' + U.fmtNumber(audience.unreachable) + ' email chưa có tài khoản sẽ KHÔNG nhận được (kênh Trong ứng dụng)';
        aAudience.appendChild(document.createTextNode(extra + '.'));
    }
    const refreshAudienceSoon = U.debounce(refreshAudience, 300);
    document.querySelectorAll('#aRoles input').forEach(function (i) { i.addEventListener('change', refreshAudienceSoon); });
    aChannel.addEventListener('change', refreshAudienceSoon);
    aMode.addEventListener('change', applyMode);
    aEmails.addEventListener('input', function () { emailsCounter(); refreshAudienceSoon(); });

    aSend.addEventListener('click', async function () {
        if (sending) return;
        showErr(aErr, '');
        const title = aTitle.value.trim(), body = aBody.value.trim();
        if (title.length < 3) return showErr(aErr, 'Tiêu đề phải từ 3 ký tự.');
        if (body.length < 5) return showErr(aErr, 'Nội dung phải từ 5 ký tự.');
        const problem = audienceProblem();
        if (problem) return showErr(aErr, problem);
        if (audience && audience.recipients === 0) return showErr(aErr, 'Không có người nhận nào với lựa chọn hiện tại.');

        const mode = aMode.value, roles = selectedRoles(), emails = parseEmails().list;
        let who;
        if (mode === 'ONLY') who = emails.length === 1 ? emails[0] : emails.length + ' email chỉ định';
        else {
            who = roles.length === 3 ? 'toàn bộ người dùng' : roles.map(function (r) { return ROLE_NAMES[r]; }).join(', ');
            if (mode === 'EXCEPT' && emails.length) who += ' (trừ ' + emails.length + ' email)';
        }
        const count = audience ? U.fmtNumber(audience.recipients) + ' người' : 'những người nhận';
        const ok = await U.confirm({
            title: 'Phát thông báo?',
            message: ['Gửi "' + title + '" tới ' + who + ' (' + count + ') qua kênh: ' + CHANNELS[aChannel.value] + '.',
                'Thông báo đã gửi không thể thu hồi.'],
            okText: 'Gửi ngay'
        });
        if (!ok) return;

        sending = true;
        U.busy(aSend, true, 'Đang gửi…');
        const res = await U.api('/api/admin/announcements', {
            method: 'POST',
            body: Object.assign({ title: title, body: body, linkUrl: aLink.value.trim() || null }, buildRequest())
        });
        U.busy(aSend, false);
        sending = false;
        if (!res.ok) { showErr(aErr, U.errMsg(res)); return; }

        U.toast('Đã gửi tới ' + U.fmtNumber(res.data.recipients) + ' người' +
            (res.data.emailQueued ? ' (' + U.fmtNumber(res.data.emailQueued) + ' email đang xếp hàng gửi)' : '') +
            (res.data.skipped ? ' · bỏ qua ' + res.data.skipped + ' email chưa có tài khoản' : '') + '.');
        aTitle.value = ''; aBody.value = ''; aLink.value = '';
        counters();
        histPage = 1;
        loadHistory();
    });

    // ---- lịch sử
    let histPage = 1, histPages = 1, histSeq = 0;
    async function loadHistory() {
        const seq = ++histSeq;
        const card = $('histCard'), list = $('histList'), state = $('histState');
        card.classList.add('is-loading');
        if (!list.firstChild) { state.hidden = true; skeleton(list, 3); }
        const res = await U.api('/api/admin/announcements?page=' + histPage + '&pageSize=10');
        if (seq !== histSeq) return;
        card.classList.remove('is-loading');
        U.clear(list);
        if (!res.ok) {
            state.hidden = false; state.className = 'ax-error'; U.clear(state);
            state.appendChild(document.createTextNode(U.errMsg(res, 'Không tải được lịch sử. ')));
            state.appendChild(el('button', { type: 'button', class: 'ax-btn ghost sm', text: 'Thử lại', onclick: loadHistory }));
            $('histPager').hidden = true;
            return;
        }
        const d = res.data;
        histPages = Math.max(1, Math.ceil(d.total / d.pageSize));
        if (d.items.length === 0) {
            if (histPage > 1) { histPage = histPages; return loadHistory(); }
            state.hidden = false; state.className = 'ax-empty'; state.textContent = 'Chưa có thông báo nào được phát.';
            $('histPager').hidden = true;
            return;
        }
        state.hidden = true;
        d.items.forEach(function (it) {
            let roles = it.custom ? 'Email chỉ định' : (it.roles.length === 3 ? 'Tất cả' : it.roles.map(function (r) { return ROLE_NAMES[r] || r; }).join(', '));
            if (!it.custom && it.excluded > 0) roles += ' (trừ ' + it.excluded + ' tài khoản)';
            const meta = U.fmtDateTime(it.sentAt) + ' · ' + roles + ' · ' + (CHANNELS[it.channel] || it.channel) +
                ' · ' + U.fmtNumber(it.inAppCount) + ' trong ứng dụng, ' + U.fmtNumber(it.emailCount) + ' email' +
                (it.sentBy ? ' · bởi ' + it.sentBy : '');
            list.appendChild(el('div', { class: 'ax-item' },
                el('div', { class: 'ax-item-main' },
                    el('p', { class: 'ax-item-title', text: it.title }),
                    el('div', { class: 'ax-item-meta', text: meta }),
                    el('details', null, el('summary', { text: 'Xem nội dung' }),
                        el('p', { text: it.body }),
                        it.linkUrl ? el('div', { class: 'ax-item-meta', text: 'Liên kết: ' + it.linkUrl }) : null))));
        });
        $('histPager').hidden = d.total <= d.pageSize;
        $('histInfo').textContent = 'Trang ' + d.page + '/' + histPages + ' · ' + U.fmtNumber(d.total) + ' thông báo';
        $('histPrev').disabled = histPage <= 1;
        $('histNext').disabled = histPage >= histPages;
    }
    $('histPrev').addEventListener('click', function () { if (histPage > 1) { histPage--; loadHistory(); } });
    $('histNext').addEventListener('click', function () { if (histPage < histPages) { histPage++; loadHistory(); } });

    // ================================================================== BANNER
    let bannerLoaded = false, banState = 'CURRENT', banSeq = 0, banItems = [], banCounts = null, banMax = 50;

    document.querySelectorAll('#banTabs .ax-tab').forEach(function (t) {
        t.addEventListener('click', function () {
            banState = t.dataset.state;
            document.querySelectorAll('#banTabs .ax-tab').forEach(function (x) { x.setAttribute('aria-selected', String(x === t)); });
            $('banHint').hidden = banState !== 'CURRENT';
            loadBanners();
        });
    });

    async function loadBanners() {
        bannerLoaded = true;
        const seq = ++banSeq;
        const card = $('banCard'), list = $('banList'), state = $('banState');
        card.classList.add('is-loading');
        if (!list.firstChild) { state.hidden = true; skeleton(list, 3); }
        const res = await U.api('/api/admin/banners?state=' + banState);
        if (seq !== banSeq) return;
        card.classList.remove('is-loading');
        if (!res.ok) {
            U.clear(list);
            state.hidden = false; state.className = 'ax-error'; U.clear(state);
            state.appendChild(document.createTextNode(U.errMsg(res, 'Không tải được banner. ')));
            state.appendChild(el('button', { type: 'button', class: 'ax-btn ghost sm', text: 'Thử lại', onclick: loadBanners }));
            return;
        }
        banItems = res.data.items; banCounts = res.data.counts; banMax = res.data.maxCurrent;
        document.querySelector('[data-n="current"]').textContent = banCounts.current;
        document.querySelector('[data-n="expired"]').textContent = banCounts.expired;
        renderBanners();
    }

    function renderBanners() {
        const list = $('banList'), state = $('banState');
        U.clear(list);
        if (banItems.length === 0) {
            state.hidden = false; state.className = 'ax-empty';
            state.textContent = banState === 'EXPIRED' ? 'Chưa có banner nào hết hạn.' : 'Chưa có banner nào đang áp dụng. Bấm "Thêm banner" để ghim sự kiện / Giveaway.';
            return;
        }
        state.hidden = true;
        const ordering = banState === 'CURRENT';
        banItems.forEach(function (b, i) {
            const st = STATES[b.state] || [b.state, 'off'];
            const buttons = [];
            if (ordering) {
                buttons.push(el('button', { type: 'button', class: 'ax-btn ghost sm', text: '▲', disabled: i === 0, attrs: { 'aria-label': 'Lên trên' }, onclick: function () { move(i, -1); } }));
                buttons.push(el('button', { type: 'button', class: 'ax-btn ghost sm', text: '▼', disabled: i === banItems.length - 1, attrs: { 'aria-label': 'Xuống dưới' }, onclick: function () { move(i, 1); } }));
            }
            if (b.state !== 'EXPIRED') {
                buttons.push(el('button', { type: 'button', class: 'ax-btn sm ' + (b.isActive ? 'ghost' : 'ok'), text: b.isActive ? 'Tắt' : 'Bật', onclick: function (e) { toggle(b, e.currentTarget); } }));
            }
            buttons.push(el('button', { type: 'button', class: 'ax-btn ghost sm', text: 'Sửa', onclick: function () { openBanner(b); } }));
            buttons.push(el('button', { type: 'button', class: 'ax-btn danger sm', text: 'Xóa', onclick: function () { remove(b); } }));

            const meta = TYPES[b.bannerType] + ' · ' + U.fmtDateTime(b.startAt) + ' → ' + U.fmtDateTime(b.endAt);
            list.appendChild(el('div', { class: 'ax-item', attrs: { 'data-id': b.id } },
                el('span', { class: 'ax-order' + (ordering ? '' : ' none'), text: ordering ? String(b.displayOrder) : '–' }),
                el('img', { class: 'ax-thumb wide', src: b.imageUrl, alt: '', loading: 'lazy' }),
                el('div', { class: 'ax-item-main' },
                    el('p', { class: 'ax-item-title', text: b.title }),
                    el('div', { class: 'ax-item-meta', text: meta }),
                    el('div', { style: 'margin-top:6px' }, el('span', { class: 'ax-badge ' + st[1], text: st[0] }))),
                el('div', { class: 'ax-actions' }, ...buttons)));
        });
    }

    function rowOf(id) { return document.querySelector('#banList .ax-item[data-id="' + id + '"]'); }
    function rowBusy(id, on) { const r = rowOf(id); if (r) r.classList.toggle('row-busy', on); }

    async function move(index, delta) {
        const j = index + delta;
        if (j < 0 || j >= banItems.length) return;
        const ids = banItems.map(function (b) { return b.id; });
        const t = ids[index]; ids[index] = ids[j]; ids[j] = t;
        $('banCard').classList.add('is-loading');
        const res = await U.api('/api/admin/banners/order', { method: 'PUT', body: { ids: ids } });
        if (!res.ok) {
            U.toast(U.errMsg(res), 'error');
            return loadBanners(); // danh sách có thể đã đổi (REORDER_MISMATCH)
        }
        banItems = res.data;
        $('banCard').classList.remove('is-loading');
        renderBanners();
    }

    async function toggle(b, btn) {
        U.busy(btn, true);
        rowBusy(b.id, true);
        const res = await U.api('/api/admin/banners/' + b.id + (b.isActive ? '/deactivate' : '/activate'), { method: 'POST' });
        if (!res.ok) { U.toast(U.errMsg(res), 'error'); rowBusy(b.id, false); U.busy(btn, false); if (res.status === 409) loadBanners(); return; }
        U.toast(b.isActive ? 'Đã tắt banner.' : 'Đã bật banner.');
        loadBanners();
    }

    async function remove(b) {
        const ok = await U.confirm({
            title: 'Xóa banner?',
            message: ['Banner "' + b.title + '" sẽ bị xóa vĩnh viễn khỏi hệ thống.', 'Nếu chỉ muốn tạm ngừng hiển thị, hãy dùng nút Tắt.'],
            okText: 'Xóa', danger: true
        });
        if (!ok) return;
        rowBusy(b.id, true);
        const res = await U.api('/api/admin/banners/' + b.id, { method: 'DELETE' });
        if (!res.ok && res.status !== 404) { U.toast(U.errMsg(res), 'error'); rowBusy(b.id, false); return; }
        U.toast('Đã xóa banner.');
        loadBanners();
    }

    // ---- hộp thoại thêm / sửa
    const modal = $('banModal');
    const bSave = $('bSave'), bErr = $('bErr');
    let editing = null, modalBusy = false;

    function setModalBusy(on) {
        modalBusy = on;
        modal.classList.toggle('is-busy', on);
        modal.querySelectorAll('input,select,textarea,button').forEach(function (n) {
            if (n === bSave) return;
            n.disabled = on;
        });
    }
    function closeModal() { if (!modalBusy) modal.close(); }
    modal.querySelectorAll('[data-close]').forEach(function (b) { b.addEventListener('click', closeModal); });
    modal.addEventListener('cancel', function (e) { if (modalBusy) e.preventDefault(); });
    modal.addEventListener('click', function (e) { if (e.target === modal) closeModal(); });

    function setPreview(url) {
        const box = $('bPreview');
        U.clear(box);
        if (!url) { box.textContent = 'Chưa có ảnh'; return; }
        box.appendChild(el('img', { src: url, alt: 'Xem trước banner' }));
    }
    $('bImage').addEventListener('change', function () { setPreview($('bImage').value.trim()); });

    function openBanner(b) {
        editing = b || null;
        showErr(bErr, '');
        $('banModalTitle').textContent = b ? 'Sửa banner' : 'Thêm banner';
        $('bTitle').value = b ? b.title : '';
        $('bType').value = b ? b.bannerType : 'EVENT';
        $('bDesc').value = b && b.description ? b.description : '';
        $('bImage').value = b ? b.imageUrl : '';
        $('bLink').value = b && b.linkUrl ? b.linkUrl : '';
        $('bStart').value = U.toLocalInput(b ? b.startAt : new Date().toISOString());
        $('bEnd').value = b ? U.toLocalInput(b.endAt) : '';
        $('bPos').value = b && b.state !== 'EXPIRED' ? b.displayOrder : '';
        $('bPos').placeholder = 'Cuối';
        $('bActive').checked = b ? b.isActive : true;
        $('bDescN').textContent = $('bDesc').value.length + '/500';
        setPreview($('bImage').value);
        modal.showModal();
        $('bTitle').focus();
    }
    $('banAdd').addEventListener('click', function () {
        if (banCounts && banCounts.current >= banMax) { U.toast('Đã đạt tối đa ' + banMax + ' banner đang áp dụng.', 'error'); return; }
        openBanner(null);
    });
    $('bDesc').addEventListener('input', function () { $('bDescN').textContent = $('bDesc').value.length + '/500'; });

    modal.querySelectorAll('[data-days]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const start = $('bStart').value ? new Date($('bStart').value) : new Date();
            const d = new Date(start.getTime() + Number(btn.dataset.days) * 86400000);
            $('bEnd').value = U.toLocalInput(d.toISOString());
        });
    });

    $('bPick').addEventListener('click', function () { $('bFile').click(); });
    $('bFile').addEventListener('change', async function () {
        const f = $('bFile').files[0];
        $('bFile').value = '';
        if (!f) return;
        if (f.size > 5 * 1024 * 1024) return showErr(bErr, 'Ảnh banner tối đa 5 MB.');
        showErr(bErr, '');
        setModalBusy(true);
        const box = $('bPreview');
        box.appendChild(el('div', { class: 'ax-overlay' }, el('span', { class: 'ax-spin' }), 'Đang tải ảnh…'));
        const fd = new FormData();
        fd.append('file', f);
        const res = await U.api('/api/admin/banners/image', { method: 'POST', body: fd });
        setModalBusy(false);
        if (!res.ok) { setPreview($('bImage').value.trim()); showErr(bErr, U.errMsg(res, 'Tải ảnh thất bại.')); return; }
        $('bImage').value = res.data.url;
        setPreview(res.data.url);
    });

    bSave.addEventListener('click', async function () {
        if (modalBusy) return;
        showErr(bErr, '');
        const title = $('bTitle').value.trim();
        if (title.length < 3) return showErr(bErr, 'Tiêu đề phải từ 3 ký tự.');
        if (!$('bImage').value.trim()) return showErr(bErr, 'Hãy tải lên hoặc nhập đường dẫn ảnh banner.');
        const endAt = U.fromLocalInput($('bEnd').value);
        if (!endAt) return showErr(bErr, 'Hãy chọn thời điểm hết hạn.');
        const startAt = U.fromLocalInput($('bStart').value);
        if (startAt && new Date(startAt) >= new Date(endAt)) return showErr(bErr, 'Thời điểm hết hạn phải sau thời điểm bắt đầu.');
        if (!editing && new Date(endAt) <= new Date()) return showErr(bErr, 'Thời điểm hết hạn phải ở tương lai.');
        const pos = $('bPos').value ? parseInt($('bPos').value, 10) : null;
        if (pos !== null && (isNaN(pos) || pos < 1)) return showErr(bErr, 'Vị trí phải từ 1 trở lên.');

        const body = {
            title: title, description: $('bDesc').value.trim() || null, imageUrl: $('bImage').value.trim(),
            linkUrl: $('bLink').value.trim() || null, bannerType: $('bType').value,
            startAt: startAt, endAt: endAt, position: pos, isActive: $('bActive').checked
        };
        setModalBusy(true);
        U.busy(bSave, true, 'Đang lưu…');
        const res = editing
            ? await U.api('/api/admin/banners/' + editing.id, { method: 'PUT', body: body })
            : await U.api('/api/admin/banners', { method: 'POST', body: body });
        U.busy(bSave, false);
        setModalBusy(false);
        if (!res.ok) { showErr(bErr, U.errMsg(res)); return; }
        modal.close();
        U.toast(editing ? 'Đã cập nhật banner.' : 'Đã thêm banner.');
        loadBanners();
    });

    // ------------------------------------------------------------------ khởi động
    counters();
    applyMode();
    loadHistory();
})();