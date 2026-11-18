// Quản lý tin tức ETOMS (Admin) — API /api/admin/news (ContentItem Kind = NEWS).
(function () {
    var STATUS = { DRAFT: ['Nháp', 'badge-soft'], PUBLISHED: ['Đã đăng', 'badge-active'], HIDDEN: ['Đã ẩn', 'badge-danger'] };
    var items = [];
    var editingId = null;
    var el = function (id) { return document.getElementById(id); };
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };

    function fmt(iso) {
        if (!iso) return '—';
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? '—' : d.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    }

    function render() {
        if (!items.length) {
            el('list').innerHTML = '<div class="empty"><h3>Chưa có tin nào</h3><p>Bấm "Viết tin mới" để đăng tin đầu tiên.</p></div>';
            return;
        }
        el('list').innerHTML = '<div class="ax-table-wrap"><table class="ax-table"><thead><tr><th>Tiêu đề</th><th>Trạng thái</th><th>Ngày đăng</th><th>Người viết</th><th class="text-end">Thao tác</th></tr></thead><tbody>' +
            items.map(function (n) {
                var s = STATUS[n.status] || [n.status, 'badge-soft'];
                return '<tr><td><b>' + esc(n.title) + '</b>' + (n.isPinned ? ' <span class="badge badge-gold">Ghim</span>' : '') +
                    '<div class="field-hint">' + esc((n.body || '').slice(0, 110)) + ((n.body || '').length > 110 ? '…' : '') + '</div></td>' +
                    '<td><span class="badge ' + s[1] + '">' + s[0] + '</span></td>' +
                    '<td>' + fmt(n.publishedAt) + '</td><td>' + esc(n.author) + '</td>' +
                    '<td class="text-end"><button type="button" class="ax-btn ghost sm" data-edit="' + n.contentItemId + '">Sửa</button>' +
                    (n.status === 'PUBLISHED' ? ' <a class="ax-btn ghost sm" href="/news/index.html?id=' + n.contentItemId + '" target="_blank" rel="noopener">Xem</a>' : '') +
                    '</td></tr>';
            }).join('') + '</tbody></table></div>';
        el('list').querySelectorAll('[data-edit]').forEach(function (b) {
            b.addEventListener('click', function () { openEditor(items.find(function (n) { return n.contentItemId === Number(b.dataset.edit); })); });
        });
    }

    async function load() {
        try { items = await Api.get('/api/admin/news'); render(); }
        catch (err) { el('list').innerHTML = '<div class="empty"><h3>Không tải được tin tức</h3><p>' + esc(err.message) + '</p></div>'; }
    }

    function openEditor(n) {
        editingId = n ? n.contentItemId : null;
        el('editor-title').textContent = n ? 'Sửa tin' : 'Viết tin mới';
        el('n-title').value = n ? n.title : '';
        el('n-body').value = n ? n.body : '';
        el('n-status').value = n ? n.status : 'PUBLISHED';
        el('n-pin').checked = !!(n && n.isPinned);
        syncPin();
        el('editor').hidden = false;
        el('editor').scrollIntoView({ block: 'start', behavior: 'smooth' });
        el('n-title').focus();
    }

    // CK_ContentItem_PinShow: chỉ ghim được tin đang đăng.
    function syncPin() {
        var ok = el('n-status').value === 'PUBLISHED';
        el('n-pin').disabled = !ok;
        if (!ok) el('n-pin').checked = false;
    }

    async function save(e) {
        e.preventDefault();
        var flash = el('flash');
        var body = { title: el('n-title').value.trim(), body: el('n-body').value.trim(), status: el('n-status').value, isPinned: el('n-pin').checked };
        if (body.title.length < 5) return Ui.showAlert(flash, 'danger', 'Tiêu đề từ 5 đến 200 ký tự.');
        if (body.body.length < 20) return Ui.showAlert(flash, 'danger', 'Nội dung tin tối thiểu 20 ký tự.');
        try {
            var res = editingId ? await Api.put('/api/admin/news/' + editingId, body) : await Api.post('/api/admin/news', body);
            Ui.showAlert(flash, 'success', res.message || 'Đã lưu tin.');
            el('editor').hidden = true;
            await load();
        } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ADMIN']);
        if (!me) return;
        el('btn-new').addEventListener('click', function () { openEditor(null); });
        el('btn-cancel').addEventListener('click', function () { el('editor').hidden = true; });
        el('n-status').addEventListener('change', syncPin);
        el('news-form').addEventListener('submit', save);
        load();
    });
})();
