/* ADM-03 – Quản lý danh mục Game */
(async function () {
    'use strict';

    const U = window.AdminUI;
    const el = U.el;
    const $ = (id) => document.getElementById(id);

    await Auth.requireAuth(['ADMIN']);
    U.renderNav('games');

    const FORMATS = {
        SINGLE_ELIMINATION: 'Loại trực tiếp',
        DOUBLE_ELIMINATION: 'Nhánh thắng/thua',
        ROUND_ROBIN: 'Vòng tròn',
        HYBRID: 'Chia bảng + loại trực tiếp'
    };
    const EVIDENCE = { IMAGE: 'Hình ảnh', VIDEO: 'Video', REPLAY: 'Replay', DOCUMENT: 'Tài liệu' };
    const MAX_LOGO = 2 * 1024 * 1024;
    const MAX_BANNER = 5 * 1024 * 1024;
    const IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/webp'];

    let status = 'ALL';
    let query = '';
    let seq = 0;
    let editing = null; // GameDetail đang sửa, null = đang thêm

    // Hộp thoại dùng thẻ <dialog> gốc (không phụ thuộc Bootstrap)
    const dlg = $('gameModal');
    const modal = { show: () => { if (!dlg.open) dlg.showModal(); }, hide: () => { if (dlg.open) dlg.close(); } };
    let dlgBusy = false; // đang gọi API trong hộp thoại: không cho đóng, khóa toàn bộ ô nhập/nút
    dlg.addEventListener('click', (e) => {
        if (dlgBusy) return;
        if (e.target === dlg || e.target.closest('[data-close]')) modal.hide(); // bấm nền hoặc nút đóng
    });
    dlg.addEventListener('cancel', (e) => { if (dlgBusy) e.preventDefault(); }); // phím Esc

    function setDialogBusy(on) {
        dlgBusy = on;
        dlg.classList.toggle('is-busy', on);
        dlg.setAttribute('aria-busy', on ? 'true' : 'false');
        dlg.querySelectorAll('input, select, button').forEach((c) => {
            if (on) { c.dataset.wasDisabled = c.disabled ? '1' : ''; c.disabled = true; }
            else { c.disabled = c.dataset.wasDisabled === '1'; }
        });
    }

    // ------------------------------------------------------------ form: dựng tùy chọn tĩnh
    Object.keys(FORMATS).forEach((k) => $('gFormat').appendChild(el('option', { value: k, text: FORMATS[k] })));
    Object.keys(EVIDENCE).forEach((k) => {
        $('gEvidence').appendChild(
            el('label', { class: 'ax-check' },
                el('input', { type: 'checkbox', value: k, attrs: { 'data-ev': k } }),
                EVIDENCE[k]));
    });

    // ------------------------------------------------------------ tải & hiển thị danh sách
    // Trạng thái đang tải: lần đầu (chưa có dòng nào) hiện khung xương; các lần sau giữ nguyên bảng,
    // làm mờ nhẹ và chạy thanh tiến trình ở mép trên để không bị nháy.
    const listCard = $('state').closest('.ax-card');
    listCard.classList.add('ax-relative');
    listCard.insertBefore(el('div', { class: 'ax-progress', attrs: { 'aria-hidden': 'true' } }), listCard.firstChild);

    function showSkeleton() {
        const body = $('rows');
        U.clear(body);
        const bar = (w, h, mt) => el('div', { class: 'ax-skel', style: 'width:' + w + 'px;height:' + h + 'px;' + (mt ? 'margin-top:' + mt + 'px' : '') });
        for (let i = 0; i < 5; i++) {
            body.appendChild(el('tr', { class: 'ax-skel-row', attrs: { 'aria-hidden': 'true' } },
                el('td', null, el('div', { class: 'd-flex align-items-center gap-3' },
                    el('div', { class: 'ax-skel ax-skel-thumb' }),
                    el('div', null, bar(130, 12), bar(46, 10, 8)))),
                el('td', null, bar(90, 12), bar(70, 10, 8)),
                el('td', null, bar(100, 12)),
                el('td', { class: 'num' }, bar(24, 12)),
                el('td', null, bar(96, 22)),
                el('td', null, bar(150, 28))));
        }
    }

    function setLoading(on) {
        const hasRows = !$('table-wrap').hidden && $('rows').querySelector('tr:not(.ax-skel-row)');
        listCard.classList.toggle('is-loading', on);
        listCard.setAttribute('aria-busy', on ? 'true' : 'false');
        if (on && !hasRows) {
            showSkeleton();
            $('table-wrap').hidden = false;
            $('state').hidden = false;
            $('state').className = 'visually-hidden'; // người dùng đọc màn hình vẫn nghe được "đang tải"
            $('state').textContent = 'Đang tải danh sách game…';
        }
    }

    async function load() {
        const my = ++seq;
        setLoading(true);

        const p = new URLSearchParams();
        if (query) p.set('q', query);
        p.set('status', status);
        const res = await U.api('/api/admin/games?' + p.toString());
        if (my !== seq) return; // bỏ phản hồi cũ (phản hồi mới hơn sẽ tắt trạng thái tải)
        setLoading(false);

        if (!res.ok) {
            U.clear($('rows'));
            $('table-wrap').hidden = true;
            $('state').hidden = false;
            $('state').className = 'ax-error';
            $('state').textContent = U.errMsg(res, 'Không tải được danh sách game.');
            return;
        }

        const c = res.data.counts;
        document.querySelector('[data-n="all"]').textContent = c.all;
        document.querySelector('[data-n="active"]').textContent = c.active;
        document.querySelector('[data-n="inactive"]').textContent = c.inactive;

        renderRows(res.data.items);
    }

    function renderRows(items) {
        const body = $('rows');
        U.clear(body);

        if (!items.length) {
            $('table-wrap').hidden = true;
            $('state').hidden = false;
            $('state').className = 'ax-empty';
            $('state').textContent = query || status !== 'ALL' ? 'Không có game nào khớp bộ lọc.' : 'Chưa có game nào. Hãy thêm game đầu tiên.';
            return;
        }

        items.forEach((g) => body.appendChild(buildRow(g)));
        $('state').hidden = true;
        $('table-wrap').hidden = false;
    }

    function buildRow(g) {
        const active = g.status === 'ACTIVE';

        const thumb = g.logoUrl
            ? el('img', { class: 'ax-thumb', src: g.logoUrl, alt: 'Logo ' + g.title, loading: 'lazy' })
            : el('div', { class: 'ax-thumb d-flex align-items-center justify-content-center', text: g.shortCode.slice(0, 3), attrs: { 'aria-hidden': 'true' } });

        const deleteBtn = el('button', {
            type: 'button', class: 'ax-btn danger sm', text: 'Xóa',
            onclick: (e) => onDelete(g, tr, e.currentTarget)
        });
        if (g.tournamentCount > 0) {
            deleteBtn.disabled = true;
            deleteBtn.title = 'Game đã có giải đấu nên không thể xóa. Hãy vô hiệu hóa.';
        }

        const toggleBtn = el('button', {
            type: 'button', class: 'ax-btn sm ' + (active ? 'ghost' : 'ok'),
            text: active ? 'Vô hiệu hóa' : 'Kích hoạt',
            onclick: (e) => onToggle(g, !active, tr, e.currentTarget)
        });

        const tr = el('tr', null,
            el('td', null,
                el('div', { class: 'd-flex align-items-center gap-3' },
                    thumb,
                    el('div', null,
                        el('div', { class: 'fw-semibold', text: g.title }),
                        el('div', { class: 'ax-code', text: g.shortCode })))),
            el('td', null,
                el('div', { text: g.minStarters === g.maxStarters ? g.maxStarters + ' chính thức' : g.minStarters + '–' + g.maxStarters + ' chính thức' }),
                el('div', { class: 'ax-help', text: g.maxSubstitutes + ' dự bị tối đa' })),
            el('td', null, FORMATS[g.defaultFormat] || g.defaultFormat),
            el('td', { class: 'num', text: U.fmtNumber(g.tournamentCount) }),
            el('td', null, el('span', { class: 'ax-badge ' + (active ? 'ok' : 'off'), text: active ? 'Đang hoạt động' : 'Vô hiệu' })),
            el('td', null,
                el('div', { class: 'ax-actions' },
                    el('button', { type: 'button', class: 'ax-btn ghost sm', text: 'Sửa', onclick: () => openEditor(g) }),
                    toggleBtn,
                    deleteBtn)));
        return tr;
    }

    // ------------------------------------------------------------ thao tác trên dòng
    /** Khóa mọi nút của dòng và hiện vòng xoay trên nút đang chạy, tránh bấm đúp. */
    function rowBusy(tr, btn, on) {
        tr.classList.toggle('row-busy', on);
        tr.querySelectorAll('button').forEach((b) => {
            if (on) { b.dataset.wasDisabled = b.disabled ? '1' : ''; b.disabled = true; }
            else { b.disabled = b.dataset.wasDisabled === '1'; }
        });
        if (!btn) return;
        if (on) {
            btn.dataset.label = btn.textContent;
            U.clear(btn);
            btn.appendChild(el('span', { class: 'ax-spin' }));
            btn.appendChild(document.createTextNode(btn.dataset.label));
        } else if (btn.dataset.label) {
            btn.textContent = btn.dataset.label;
        }
    }

    async function onToggle(g, makeActive, tr, btn) {
        if (!makeActive) {
            const ok = await U.confirm({
                title: 'Vô hiệu hóa game?',
                message: ['“' + g.title + '” sẽ bị vô hiệu hóa. Các giải đấu đã tạo không bị ảnh hưởng.'],
                okText: 'Vô hiệu hóa', danger: true
            });
            if (!ok) return;
        }
        rowBusy(tr, btn, true);
        let res;
        try { res = await U.api('/api/admin/games/' + g.gameId + (makeActive ? '/activate' : '/deactivate'), { method: 'POST' }); }
        finally { rowBusy(tr, btn, false); }
        if (res.ok) { U.toast(makeActive ? 'Đã kích hoạt game.' : 'Đã vô hiệu hóa game.'); load(); }
        else { U.toast(U.errMsg(res), 'error'); load(); }
    }

    async function onDelete(g, tr, btn) {
        const ok = await U.confirm({
            title: 'Xóa game vĩnh viễn?',
            message: ['Xóa “' + g.title + '” sẽ không thể hoàn tác.', 'Chỉ xóa được game chưa có giải đấu hay dữ liệu liên quan.'],
            okText: 'Xóa', danger: true
        });
        if (!ok) return;
        rowBusy(tr, btn, true);
        let res;
        try { res = await U.api('/api/admin/games/' + g.gameId, { method: 'DELETE' }); }
        finally { rowBusy(tr, btn, false); }
        if (res.ok) { U.toast('Đã xóa game.'); load(); }
        else { U.toast(U.errMsg(res), 'error'); load(); }
    }

    // ------------------------------------------------------------ hộp thoại thêm / sửa
    function setError(msg) {
        $('gErr').hidden = !msg;
        $('gErr').textContent = msg || '';
    }

    function fillForm(g) {
        $('gTitle').value = g ? g.title : '';
        $('gCode').value = g ? g.shortCode : '';
        $('gMin').value = g ? g.minStarters : 1;
        $('gMax').value = g ? g.maxStarters : 5;
        $('gSubs').value = g ? g.maxSubstitutes : 1;
        $('gFormat').value = g ? g.defaultFormat : 'SINGLE_ELIMINATION';
        $('gFileMb').value = g ? g.maxEvidenceFileSizeMb : 10;
        const picked = g ? g.allowedEvidenceTypes : ['IMAGE'];
        $('gEvidence').querySelectorAll('input[type=checkbox]').forEach((cb) => { cb.checked = picked.indexOf(cb.value) >= 0; });
    }

    function renderMedia() {
        const isEdit = !!editing;
        $('mediaSection').hidden = !isEdit;
        $('mediaHint').hidden = isEdit;
        if (!isEdit) return;
        paintPreview('logoPreview', editing.logoUrl, 'Chưa có logo', 'Logo ' + editing.title);
        paintPreview('bannerPreview', editing.bannerUrl, 'Chưa có banner', 'Banner ' + editing.title);
        $('logoClear').hidden = !editing.logoUrl;
        $('bannerClear').hidden = !editing.bannerUrl;
    }

    function paintPreview(id, url, emptyText, alt) {
        const box = $(id);
        U.clear(box);
        if (url) box.appendChild(el('img', { src: url, alt: alt }));
        else box.textContent = emptyText;
    }

    function openEditor(g) {
        editing = g || null;
        $('gameModalTitle').textContent = g ? 'Sửa game: ' + g.title : 'Thêm game';
        fillForm(g);
        setError('');
        renderMedia();
        modal.show();
        setTimeout(() => $('gTitle').focus(), 250);
    }

    function readForm() {
        const evidence = [];
        $('gEvidence').querySelectorAll('input[type=checkbox]').forEach((cb) => { if (cb.checked) evidence.push(cb.value); });
        return {
            title: $('gTitle').value.trim(),
            shortCode: $('gCode').value.trim().toUpperCase(),
            minStarters: parseInt($('gMin').value, 10) || 0,
            maxStarters: parseInt($('gMax').value, 10) || 0,
            maxSubstitutes: parseInt($('gSubs').value, 10),
            defaultFormat: $('gFormat').value,
            allowedEvidenceTypes: evidence,
            maxEvidenceFileSizeMb: parseInt($('gFileMb').value, 10) || 0
        };
    }

    function validateLocal(b) {
        if (b.title.length < 2 || b.title.length > 150) return 'Tên game phải từ 2 đến 150 ký tự.';
        if (!/^[A-Z0-9_]{2,10}$/.test(b.shortCode)) return 'Mã game gồm 2–10 ký tự A–Z, 0–9 hoặc gạch dưới.';
        if (b.minStarters < 1 || b.maxStarters < b.minStarters) return 'Số người tối đa phải lớn hơn hoặc bằng số người tối thiểu (≥ 1).';
        if (isNaN(b.maxSubstitutes) || b.maxSubstitutes < 0) return 'Số dự bị phải từ 0 trở lên.';
        if (!b.allowedEvidenceTypes.length) return 'Chọn ít nhất một loại bằng chứng.';
        if (b.maxEvidenceFileSizeMb < 1 || b.maxEvidenceFileSizeMb > 100) return 'Dung lượng bằng chứng phải từ 1 đến 100 MB.';
        return '';
    }

    async function onSave() {
        const body = readForm();
        const bad = validateLocal(body);
        if (bad) { setError(bad); return; }
        setError('');

        setDialogBusy(true);
        U.busy($('gSave'), true, 'Đang lưu…');
        const isEdit = !!editing;
        let res;
        try {
            res = await U.api(isEdit ? '/api/admin/games/' + editing.gameId : '/api/admin/games',
                { method: isEdit ? 'PUT' : 'POST', body: body });
        } finally {
            U.busy($('gSave'), false);
            setDialogBusy(false);
        }

        if (!res.ok) { setError(U.errMsg(res, 'Không lưu được game.')); return; }

        U.toast(isEdit ? 'Đã cập nhật game.' : 'Đã thêm game. Bạn có thể tải Logo và Banner bên dưới.');
        editing = res.data;
        $('gameModalTitle').textContent = 'Sửa game: ' + editing.title;
        renderMedia(); // sau khi tạo: chuyển sang chế độ sửa để tải ảnh
        load();
    }

    // ------------------------------------------------------------ tải ảnh
    /** Phủ vòng xoay lên ô xem trước và khóa hộp thoại trong lúc gọi API. */
    async function withMediaBusy(kind, text, task) {
        const box = $(kind + 'Preview');
        box.appendChild(el('div', { class: 'ax-overlay', attrs: { role: 'status' } }, el('span', { class: 'ax-spin lg' }), text));
        setDialogBusy(true);
        try { return await task(); }
        finally { setDialogBusy(false); renderMedia(); } // renderMedia vẽ lại ô xem trước, tự gỡ lớp phủ
    }

    async function uploadImage(kind, file, limit, inputEl) {
        if (!file || !editing) return;
        if (IMAGE_TYPES.indexOf(file.type) < 0) { setError('Chỉ chấp nhận ảnh PNG, JPG hoặc WebP.'); inputEl.value = ''; return; }
        if (file.size > limit) { setError('Ảnh vượt quá ' + (limit / 1048576) + ' MB.'); inputEl.value = ''; return; }
        setError('');

        const fd = new FormData();
        fd.append('file', file);
        const res = await withMediaBusy(kind, 'Đang tải lên…',
            () => U.api('/api/admin/games/' + editing.gameId + '/' + kind, { method: 'POST', body: fd }));
        inputEl.value = '';
        if (!res.ok) { setError(U.errMsg(res, 'Không tải được ảnh.')); return; }
        editing = res.data;
        renderMedia();
        U.toast(kind === 'logo' ? 'Đã cập nhật logo.' : 'Đã cập nhật banner.');
        load();
    }

    async function clearImage(kind) {
        if (!editing) return;
        const res = await withMediaBusy(kind, 'Đang gỡ…',
            () => U.api('/api/admin/games/' + editing.gameId + '/' + kind, { method: 'DELETE' }));
        if (!res.ok) { setError(U.errMsg(res, 'Không gỡ được ảnh.')); return; }
        editing = res.data;
        renderMedia();
        U.toast('Đã gỡ ảnh.');
        load();
    }

    // ------------------------------------------------------------ sự kiện
    $('btn-add').addEventListener('click', () => openEditor(null));
    $('gSave').addEventListener('click', onSave);
    $('gameForm').addEventListener('submit', (e) => { e.preventDefault(); onSave(); });

    $('logoPick').addEventListener('click', () => $('logoFile').click());
    $('bannerPick').addEventListener('click', () => $('bannerFile').click());
    $('logoFile').addEventListener('change', (e) => uploadImage('logo', e.target.files[0], MAX_LOGO, e.target));
    $('bannerFile').addEventListener('change', (e) => uploadImage('banner', e.target.files[0], MAX_BANNER, e.target));
    $('logoClear').addEventListener('click', () => clearImage('logo'));
    $('bannerClear').addEventListener('click', () => clearImage('banner'));

    $('q').addEventListener('input', U.debounce((e) => { query = e.target.value.trim(); load(); }, 350));

    $('tabs').addEventListener('click', (e) => {
        const t = e.target.closest('.ax-tab');
        if (!t) return;
        status = t.dataset.status;
        $('tabs').querySelectorAll('.ax-tab').forEach((x) => x.setAttribute('aria-selected', x === t ? 'true' : 'false'));
        load();
    });

    load();
})();