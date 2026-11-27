// UC-13 A4 — Ban tổ chức đăng / sửa / ẩn tin của giải. Tin đã đăng hiện ở trang giải và trang Tin tức.
(function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var STATUS = { DRAFT: 'Nháp', PUBLISHED: 'Đã đăng', HIDDEN: 'Đã ẩn' };
    var tournamentId, data = null, editingId = null, flash;

    function fmt(iso) {
        if (!iso) return '';
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? '' : d.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    }

    function showEmpty(title, text) {
        document.getElementById('news-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>' + esc(title) + '</h3><p>' + esc(text || '') + '</p></div></div>';
    }

    function formHtml() {
        var n = editingId ? data.items.find(function (x) { return x.contentItemId === editingId; }) : null;
        var status = n ? n.status : 'PUBLISHED';
        return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">' + (n ? 'Sửa tin' : 'Viết tin mới') + '</div>' +
            '<form id="news-form" novalidate>' +
            '<div class="field"><label for="nf-title">Tiêu đề <span class="req">*</span></label>' +
            '<input id="nf-title" name="Title" class="input" maxlength="200" value="' + esc(n ? n.title : '') + '" />' +
            '<span class="field-error" data-field-error="Title"></span></div>' +
            '<div class="field"><label for="nf-body">Nội dung <span class="req">*</span></label>' +
            '<textarea id="nf-body" name="Body" class="input" rows="8" maxlength="20000" placeholder="Mỗi đoạn cách nhau một dòng trống.">' + esc(n ? n.body : '') + '</textarea>' +
            '<span class="field-error" data-field-error="Body"></span></div>' +
            '<div class="field"><label for="nf-status">Trạng thái</label><select id="nf-status" name="Status" class="select">' +
            ['PUBLISHED', 'DRAFT', 'HIDDEN'].map(function (s) { return '<option value="' + s + '"' + (s === status ? ' selected' : '') + '>' + STATUS[s] + '</option>'; }).join('') +
            '</select></div>' +
            '<div class="btn-row"><button type="submit" class="btn btn-primary">' + (n ? 'Lưu tin' : 'Đăng tin') + '</button>' +
            (n ? '<button type="button" class="btn btn-ghost" id="nf-cancel">Hủy sửa</button>' : '') + '</div></form></div></section>';
    }

    function listHtml() {
        if (!data.items.length) return '<section class="card-etoms"><div class="empty"><h3>Chưa có tin nào</h3><p>Tin đã đăng sẽ hiện ở trang giải và trang Tin tức.</p></div></section>';
        return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Tin của giải (' + data.items.length + ')</div><div data-news-list></div></div></section>';
    }
    function newsRowHtml(n) {
                return '<div class="member-card"><div class="grow min-w-0"><div class="nm text-truncate">' + esc(n.title) + '</div>' +
                    '<div class="rl">' + STATUS[n.status] + ' · ' + fmt(n.publishedAt || n.updatedAt) + '</div></div>' +
                    (n.status === 'PUBLISHED' ? '<a class="btn btn-ghost btn-sm" href="/news/index.html?id=' + n.contentItemId + '">Xem</a>' : '') +
                    '<button type="button" class="btn btn-ghost btn-sm" data-edit="' + n.contentItemId + '">Sửa</button></div>';
    }

    function render() {
        document.getElementById('news-root').innerHTML = '<div class="d-flex flex-column gap-3">' + formHtml() + listHtml() + '</div>';
        // Nhiều tin: tìm theo tiêu đề + phân trang 8 tin (góp ý 27/11). Nút Sửa gắn sự kiện ở news-root.
        Ui.listPager(document.querySelector('#news-root [data-news-list]'), {
            items: data.items, pageSize: 8, listClass: 'd-grid gap-2', render: newsRowHtml, placeholder: 'Tìm tin theo tiêu đề...',
            text: function (n) { return n.title + ' ' + STATUS[n.status]; }
        });
        var form = document.getElementById('news-form');
        var cancel = document.getElementById('nf-cancel');
        if (cancel) cancel.addEventListener('click', function () { editingId = null; render(); });
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash); Ui.clearFieldErrors(form);
            var body = { Title: form.Title.value.trim(), Body: form.Body.value.trim(), Status: form.Status.value };
            var errors = {};
            if (!body.Title) errors.Title = 'Vui lòng nhập đầy đủ thông tin bắt buộc.';
            if (!body.Body) errors.Body = 'Vui lòng nhập đầy đủ thông tin bắt buộc.';
            if (Object.keys(errors).length) { Ui.applyFieldErrors(form, errors); return; }
            try {
                var url = '/api/tournaments/' + tournamentId + '/news' + (editingId ? '/' + editingId : '');
                var res = editingId ? await Api.put(url, body) : await Api.post(url, body);
                editingId = null;
                await load();
                Ui.showAlert(flash, 'success', res.message);
            } catch (err) { Ui.applyFieldErrors(form, err.fieldErrors); Ui.showAlert(flash, 'danger', err.message || 'Không lưu được tin.'); }
        });
    }

    async function load() {
        try { data = (await Api.get('/api/tournaments/' + tournamentId + '/news')).data; }
        catch (err) { showEmpty(err.status === 404 ? 'Không tìm thấy giải đấu' : 'Không tải được tin của giải', err.message); return; }
        document.getElementById('page-title').textContent = 'Tin tức giải · ' + data.tournamentName;
        if (!data.isOrganizer) { showEmpty('Bạn không quản lý giải này', 'Chỉ Ban tổ chức của giải đăng được tin cho giải.'); return; }
        render();
    }

    document.addEventListener('DOMContentLoaded', async function () {
        flash = document.getElementById('flash');
        var me = await Ui.requireAuth(['ORGANIZER']);
        if (!me) return;
        tournamentId = Ui.qsInt('id');
        if (!tournamentId || tournamentId < 1) { showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.'); return; }
        document.getElementById('news-root').addEventListener('click', function (e) {
            var b = e.target.closest('[data-edit]');
            if (!b) return;
            editingId = parseInt(b.dataset.edit, 10);
            render();
            document.getElementById('news-form').scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
        await load();
    });
})();
