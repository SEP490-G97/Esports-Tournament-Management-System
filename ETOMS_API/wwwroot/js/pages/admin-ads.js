// Quảng cáo & thông báo (Admin): banner trang chủ (/api/admin/banners) + thông báo hệ thống (/api/admin/announcements).
(function () {
    var el = function (id) { return document.getElementById(id); };
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var TYPE = { EVENT: 'Sự kiện', GIVEAWAY: 'Quà tặng' };
    var STATE = { LIVE: ['Đang hiển thị', 'badge-active'], SCHEDULED: ['Chờ tới giờ', 'badge-soft'], EXPIRED: ['Đã hết hạn', 'badge-soft'], INACTIVE: ['Đã tắt', 'badge-danger'] };
    var TARGET = { ALL: 'Mọi tài khoản', PLAYER: 'Tuyển thủ', ORGANIZER: 'Ban tổ chức' };
    var CHANNEL = { IN_APP: 'Trong web', EMAIL: 'Email', BOTH: 'Trong web và email' };
    var banners = [], editingId = null;

    function toDate(iso) { return new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z'); }
    function fmt(iso) {
        if (!iso) return '—';
        var d = toDate(iso);
        return isNaN(d.getTime()) ? '—' : d.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    }
    // Giá trị cho ô datetime-local theo giờ máy người dùng.
    function localInput(d) {
        var p = function (n) { return String(n).padStart(2, '0'); };
        return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) + 'T' + p(d.getHours()) + ':' + p(d.getMinutes());
    }

    // ---------------------------------------------------------------- Tab
    function selectTab(which) {
        ['banners', 'ann', 'tpl'].forEach(function (k) {
            el('tab-' + k).setAttribute('aria-selected', k === which ? 'true' : 'false');
            el('panel-' + k).hidden = k !== which;
        });
        Ui.clearAlert(el('flash'));
        if (which === 'ann') { loadAnnouncements(); loadTemplates(); }
        if (which === 'tpl') loadTemplates();
    }

    // ---------------------------------------------------------------- Mẫu nội dung (NUI-NOT-01)
    var templates = [];
    function fillTemplateForm() {
        var t = templates.find(function (x) { return x.type === el('t-type').value; }) || {};
        el('t-title').value = t.title || '';
        el('t-body').value = t.body || '';
        el('t-status').value = t.status === 'HIDDEN' ? 'HIDDEN' : 'PUBLISHED';
    }
    async function loadTemplates() {
        try {
            var res = await Api.get('/api/admin/notification-templates');
            templates = res.templates || [];
            el('tpl-vars').textContent = (res.variables || []).join(', ');
            var current = el('t-type').value;
            el('t-type').innerHTML = templates.map(function (t) { return '<option value="' + esc(t.type) + '">' + esc(t.label) + '</option>'; }).join('');
            if (current) el('t-type').value = current;
            fillTemplateForm();
            el('a-template').innerHTML = '<option value="">Không dùng mẫu</option>' + templates.filter(function (t) { return t.title; }).map(function (t) {
                return '<option value="' + esc(t.type) + '">' + esc(t.label) + (t.status === 'HIDDEN' ? ' (đang tắt)' : '') + '</option>';
            }).join('');
            el('tpl-list').innerHTML = '<div class="table-wrap"><table class="data-table"><thead><tr><th>Loại</th><th>Tiêu đề</th><th>Trạng thái</th><th>Cập nhật</th></tr></thead><tbody>' +
                templates.map(function (t) {
                    return '<tr><td>' + esc(t.label) + '</td><td>' + (t.title ? esc(t.title) : '<span class="field-hint">Chưa có mẫu (email dùng nội dung mặc định)</span>') + '</td><td>' +
                        (t.title ? (t.status === 'PUBLISHED' ? 'Đang dùng' : 'Tắt') : '—') + '</td><td>' + fmt(t.updatedAt) + '</td></tr>';
                }).join('') + '</tbody></table></div>';
        } catch (err) { el('tpl-list').innerHTML = '<div class="empty"><h3>Không tải được mẫu</h3><p>' + esc(err.message) + '</p></div>'; }
    }
    async function saveTemplate(e) {
        e.preventDefault();
        var flash = el('flash');
        var body = { title: el('t-title').value.trim(), body: el('t-body').value.trim(), status: el('t-status').value };
        if (body.title.length < 3) return Ui.showAlert(flash, 'danger', 'Tiêu đề mẫu từ 3 đến 200 ký tự.');
        if (body.body.length < 5) return Ui.showAlert(flash, 'danger', 'Nội dung mẫu từ 5 đến 5000 ký tự.');
        try {
            var res = await Api.put('/api/admin/notification-templates/' + encodeURIComponent(el('t-type').value), body);
            Ui.showAlert(flash, 'success', res.message);
            loadTemplates();
        } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
    }
    // Thông báo hệ thống bắt đầu soạn từ mẫu: điền tiêu đề / nội dung (bỏ các biến chỉ dùng cho email tự động).
    function applyTemplateToAnnouncement() {
        var t = templates.find(function (x) { return x.type === el('a-template').value; });
        if (!t) return;
        var strip = function (s) { return String(s || '').replace(/\{(ten|tieu_de|noi_dung|lien_ket)\}/g, '').trim(); };
        el('a-title').value = strip(t.title).slice(0, 200);
        el('a-body').value = strip(t.body).slice(0, 1000);
    }

    // ---------------------------------------------------------------- Banner
    function renderBanners() {
        if (!banners.length) {
            el('banner-list').innerHTML = '<div class="empty"><h3>Chưa có banner nào</h3><p>Bấm "Thêm banner" để đăng banner đầu tiên.</p></div>';
            return;
        }
        el('banner-list').innerHTML = '<div class="table-wrap"><table class="data-table"><thead><tr><th>Ảnh</th><th>Tiêu đề</th><th>Thời gian hiển thị</th><th>Trạng thái</th><th class="text-end">Thao tác</th></tr></thead><tbody>' +
            banners.map(function (b) {
                var s = STATE[b.state] || [b.state, 'badge-soft'];
                return '<tr><td><img class="banner-thumb" src="' + esc(Ui.safeUrl(b.imageUrl)) + '" alt=""></td>' +
                    '<td><b>' + esc(b.title) + '</b> <span class="badge badge-gold">' + esc(TYPE[b.bannerType] || b.bannerType) + '</span>' +
                    '<div class="field-hint">Thứ tự ' + b.displayOrder + (b.linkUrl ? ' · ' + esc(b.linkUrl) : '') + '</div></td>' +
                    '<td>' + fmt(b.startAt) + '<br>→ ' + fmt(b.endAt) + '</td>' +
                    '<td><span class="badge ' + s[1] + '">' + s[0] + '</span></td>' +
                    '<td class="text-end text-nowrap">' +
                    (b.state === 'LIVE' ? '<a class="btn btn-ghost btn-sm" href="/events/index.html?id=' + b.systemBannerId + '" target="_blank" rel="noopener">Xem</a> ' : '') +
                    '<button type="button" class="btn btn-ghost btn-sm" data-edit="' + b.systemBannerId + '">Sửa</button> ' +
                    '<button type="button" class="btn btn-ghost btn-sm" data-toggle="' + b.systemBannerId + '">' + (b.isActive ? 'Tắt' : 'Bật') + '</button> ' +
                    '<button type="button" class="btn btn-ghost btn-sm" data-del="' + b.systemBannerId + '">Xóa</button></td></tr>';
            }).join('') + '</tbody></table></div>';
    }

    async function loadBanners() {
        try { banners = await Api.get('/api/admin/banners'); renderBanners(); }
        catch (err) { el('banner-list').innerHTML = '<div class="empty"><h3>Không tải được banner</h3><p>' + esc(err.message) + '</p></div>'; }
    }

    function openEditor(b) {
        editingId = b ? b.systemBannerId : null;
        el('banner-editor-title').textContent = b ? 'Sửa banner' : 'Thêm banner';
        el('b-title').value = b ? b.title : '';
        el('b-desc').value = b && b.description ? b.description : '';
        el('b-type').value = b ? b.bannerType : 'EVENT';
        el('b-link').value = b && b.linkUrl ? b.linkUrl : '';
        el('b-order').value = b ? b.displayOrder : 1;
        el('b-active').checked = b ? !!b.isActive : true;
        el('b-file').value = '';
        el('b-imglink').value = '';
        el('b-start').value = localInput(b ? toDate(b.startAt) : new Date());
        el('b-end').value = localInput(b ? toDate(b.endAt) : new Date(Date.now() + 14 * 86400000));
        el('banner-editor').hidden = false;
        el('banner-editor').scrollIntoView({ block: 'start', behavior: 'smooth' });
        el('b-title').focus();
    }

    async function saveBanner(e) {
        e.preventDefault();
        var flash = el('flash');
        var title = el('b-title').value.trim();
        if (title.length < 3) return Ui.showAlert(flash, 'danger', 'Tiêu đề banner từ 3 đến 200 ký tự.');
        if (!el('b-start').value || !el('b-end').value) return Ui.showAlert(flash, 'danger', 'Chọn thời gian bắt đầu và kết thúc hiển thị.');
        var start = new Date(el('b-start').value), end = new Date(el('b-end').value);
        if (start >= end) return Ui.showAlert(flash, 'danger', 'Thời gian kết thúc phải sau thời gian bắt đầu.');
        var file = el('b-file').files[0], imgLink = el('b-imglink').value.trim();
        if (!editingId && !file && !imgLink) return Ui.showAlert(flash, 'danger', 'Chọn ảnh banner (tải ảnh lên hoặc dán liên kết ảnh).');
        if (file && file.size > 5 * 1024 * 1024) return Ui.showAlert(flash, 'danger', 'Ảnh tối đa 5 MB.');

        var fd = new FormData();
        fd.append('Title', title);
        fd.append('Description', el('b-desc').value.trim());
        fd.append('BannerType', el('b-type').value);
        fd.append('LinkUrl', el('b-link').value.trim());
        fd.append('StartAt', start.toISOString());
        fd.append('EndAt', end.toISOString());
        fd.append('DisplayOrder', el('b-order').value || '1');
        fd.append('IsActive', el('b-active').checked ? 'true' : 'false');
        if (file) fd.append('ImageFile', file);
        else if (imgLink) fd.append('ImageLink', imgLink);
        try {
            var res = editingId ? await Api.putForm('/api/admin/banners/' + editingId, fd) : await Api.postForm('/api/admin/banners', fd);
            Ui.showAlert(flash, 'success', res.message || 'Đã lưu banner.');
            el('banner-editor').hidden = true;
            await loadBanners();
        } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
    }

    async function onBannerClick(e) {
        var t = e.target.closest('button[data-edit],button[data-toggle],button[data-del]');
        if (!t) return;
        var flash = el('flash');
        if (t.dataset.edit) return openEditor(banners.find(function (b) { return b.systemBannerId === Number(t.dataset.edit); }));
        try {
            if (t.dataset.toggle) {
                var r1 = await Api.post('/api/admin/banners/' + t.dataset.toggle + '/toggle', {});
                Ui.showAlert(flash, 'success', r1.message);
            } else {
                var b = banners.find(function (x) { return x.systemBannerId === Number(t.dataset.del); });
                if (!await window.etomsConfirm('Xóa banner "' + (b ? b.title : '') + '"? Không khôi phục được.', { title: 'Xóa banner', okText: 'Xóa' })) return;
                var r2 = await Api.del('/api/admin/banners/' + t.dataset.del);
                Ui.showAlert(flash, 'success', r2.message);
            }
            await loadBanners();
        } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
    }

    // ---------------------------------------------------------------- Thông báo hệ thống
    async function loadAnnouncements() {
        try {
            var rows = await Api.get('/api/admin/announcements');
            if (!rows.length) { el('ann-list').innerHTML = '<div class="empty"><h3>Chưa gửi thông báo nào</h3></div>'; return; }
            el('ann-list').innerHTML = '<div class="table-wrap"><table class="data-table"><thead><tr><th>Thông báo</th><th>Người nhận</th><th>Kênh</th><th class="text-end">Đã gửi</th><th>Thời gian</th></tr></thead><tbody>' +
                rows.map(function (a) {
                    return '<tr><td><b>' + esc(a.title) + '</b><div class="field-hint">' + esc((a.body || '').slice(0, 120)) + ((a.body || '').length > 120 ? '…' : '') + '</div></td>' +
                        '<td>' + esc(TARGET[a.targetRoles] || a.targetRoles) + '</td><td>' + esc(CHANNEL[a.channel] || a.channel) + '</td>' +
                        '<td class="text-end text-nowrap">' + a.inAppCount + ' trong web<br>' + a.emailCount + ' email</td>' +
                        '<td>' + fmt(a.sentAt) + '<div class="field-hint">' + esc(a.sentBy) + '</div></td></tr>';
                }).join('') + '</tbody></table></div>';
        } catch (err) { el('ann-list').innerHTML = '<div class="empty"><h3>Không tải được lịch sử</h3><p>' + esc(err.message) + '</p></div>'; }
    }

    async function sendAnnouncement(e) {
        e.preventDefault();
        var flash = el('flash');
        var body = {
            title: el('a-title').value.trim(), body: el('a-body').value.trim(), linkUrl: el('a-link').value.trim(),
            targetRoles: el('a-target').value, channel: el('a-channel').value
        };
        if (body.title.length < 3) return Ui.showAlert(flash, 'danger', 'Tiêu đề từ 3 đến 200 ký tự.');
        if (body.body.length < 5) return Ui.showAlert(flash, 'danger', 'Nội dung từ 5 đến 1000 ký tự.');
        if (body.linkUrl && body.linkUrl[0] !== '/') return Ui.showAlert(flash, 'danger', 'Liên kết phải là đường dẫn trong ETOMS (bắt đầu bằng /).');
        var who = TARGET[body.targetRoles].toLowerCase(), how = CHANNEL[body.channel].toLowerCase();
        if (!await window.etomsConfirm('Gửi thông báo "' + body.title + '" tới ' + who + ' qua kênh ' + how + '? Đã gửi thì không thu hồi được.', { title: 'Gửi thông báo', okText: 'Gửi' })) return;
        try {
            var res = await Api.post('/api/admin/announcements', body);
            Ui.showAlert(flash, 'success', res.message);
            el('ann-form').reset();
            loadAnnouncements();
        } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ADMIN']);
        if (!me) return;
        el('tab-banners').addEventListener('click', function () { selectTab('banners'); });
        el('tab-ann').addEventListener('click', function () { selectTab('ann'); });
        el('btn-new-banner').addEventListener('click', function () { openEditor(null); });
        el('b-cancel').addEventListener('click', function () { el('banner-editor').hidden = true; });
        el('banner-form').addEventListener('submit', saveBanner);
        el('banner-list').addEventListener('click', onBannerClick);
        el('ann-form').addEventListener('submit', sendAnnouncement);
        el('tab-tpl').addEventListener('click', function () { selectTab('tpl'); });
        el('t-type').addEventListener('change', fillTemplateForm);
        el('tpl-form').addEventListener('submit', saveTemplate);
        el('a-template').addEventListener('change', applyTemplateToAnnouncement);
        if (location.hash === '#thong-bao') selectTab('ann');
        if (location.hash === '#mau-noi-dung') selectTab('tpl');
        loadBanners();
    });
})();
