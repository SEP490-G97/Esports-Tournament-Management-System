// UC-42 — Ban tổ chức thêm, sửa, gỡ nhà tài trợ của giải (tên, hạng, logo, trang web, liên hệ, thứ tự,
// có hiện trên giải đã hủy không — BR-SPN-02). Kiểm ở trình duyệt trùng thông báo máy chủ; máy chủ kiểm lại toàn bộ.
(function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var tournamentId, data = null, editingId = null, flash;
    var TIERS = [['DIAMOND', 'Kim cương'], ['GOLD', 'Vàng'], ['SILVER', 'Bạc'], ['BRONZE', 'Đồng'], ['PARTNER', 'Đối tác']];

    function showEmpty(title, text) {
        document.getElementById('spn-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>' + esc(title) + '</h3><p>' + esc(text || '') + '</p></div></div>';
    }

    function validate(form) {
        var errors = {};
        var name = form.Name.value.trim();
        if (!name) errors.Name = 'Vui lòng nhập tên nhà tài trợ.';
        else if (name.length > 200) errors.Name = 'Tên nhà tài trợ tối đa 200 ký tự.';
        var site = form.WebsiteUrl.value.trim();
        if (site) { try { var u = new URL(site); if (!/^https?:$/.test(u.protocol)) throw 0; } catch (e) { errors.WebsiteUrl = 'Trang web phải là địa chỉ http(s) hợp lệ, tối đa 500 ký tự.'; } }
        var order = form.DisplayOrder.value.trim();
        if (order && (!/^\d+$/.test(order) || +order < 1 || +order > 999)) errors.DisplayOrder = 'Thứ tự hiển thị từ 1 đến 999.';
        var file = form.logo.files[0];
        if (file && file.size > 2 * 1024 * 1024) errors.logo = 'Ảnh không được vượt quá 2MB.';
        return errors;
    }

    function formHtml() {
        var s = editingId ? data.sponsors.find(function (x) { return x.tournamentSponsorId === editingId; }) : null;
        var tier = s ? s.tier : 'GOLD';
        return '<section class="card-etoms"><div class="body">' +
            '<div class="section-title mt-0">' + (s ? 'Sửa nhà tài trợ' : 'Thêm nhà tài trợ') + '</div>' +
            '<form id="spn-form" novalidate>' +
            '<div class="row g-3"><div class="col-12 col-md-7"><div class="field mb-0"><label for="sp-name">Tên nhà tài trợ <span class="req">*</span></label>' +
            '<input id="sp-name" name="Name" class="input" maxlength="200" value="' + esc(s ? s.name : '') + '" />' +
            '<span class="field-error" data-field-error="Name"></span></div></div>' +
            '<div class="col-6 col-md-3"><div class="field mb-0"><label for="sp-tier">Hạng <span class="req">*</span></label><select id="sp-tier" name="Tier" class="select">' +
            TIERS.map(function (t) { return '<option value="' + t[0] + '"' + (t[0] === tier ? ' selected' : '') + '>' + t[1] + '</option>'; }).join('') +
            '</select><span class="field-error" data-field-error="Tier"></span></div></div>' +
            '<div class="col-6 col-md-2"><div class="field mb-0"><label for="sp-order">Thứ tự</label>' +
            '<input id="sp-order" name="DisplayOrder" type="number" min="1" max="999" class="input" value="' + (s ? s.displayOrder : '') + '" />' +
            '<span class="field-error" data-field-error="DisplayOrder"></span></div></div></div>' +
            '<div class="row g-3 mt-0"><div class="col-12 col-md-6"><div class="field mb-0"><label for="sp-site">Trang web</label>' +
            '<input id="sp-site" name="WebsiteUrl" class="input" maxlength="500" placeholder="https://..." value="' + esc(s ? s.websiteUrl : '') + '" />' +
            '<span class="field-error" data-field-error="WebsiteUrl"></span></div></div>' +
            '<div class="col-12 col-md-6"><div class="field mb-0"><label for="sp-contact">Liên hệ (chỉ Ban tổ chức thấy)</label>' +
            '<input id="sp-contact" name="ContactInfo" class="input" maxlength="300" value="' + esc(s ? s.contactInfo : '') + '" />' +
            '<span class="field-error" data-field-error="ContactInfo"></span></div></div></div>' +
            '<div class="field mt-3"><label for="sp-logo">Logo (JPG, PNG, WEBP, tối đa 2MB)</label>' +
            '<input id="sp-logo" name="logo" type="file" accept="image/png,image/jpeg,image/webp" class="input" />' +
            (s && s.logoUrl ? '<label class="d-flex align-items-center gap-2 mt-2"><input type="checkbox" name="RemoveLogo" /> Gỡ logo hiện tại</label>' : '') +
            '<span class="field-error" data-field-error="logo"></span></div>' +
            '<label class="d-flex align-items-center gap-2"><input type="checkbox" name="ShowWhenCancelled"' + (!s || s.showWhenCancelled ? ' checked' : '') + ' /> Vẫn hiện logo nếu giải bị hủy</label>' +
            '<div class="btn-row mt-3"><button type="submit" class="btn btn-primary">' + (s ? 'Lưu thay đổi' : 'Thêm nhà tài trợ') + '</button>' +
            (s ? '<button type="button" class="btn btn-ghost" id="sp-cancel">Hủy sửa</button>' : '') + '</div>' +
            '</form></div></section>';
    }

    function listHtml() {
        if (!data.sponsors.length)
            return '<section class="card-etoms"><div class="empty"><h3>Chưa có nhà tài trợ</h3><p>Thêm nhà tài trợ đầu tiên ở khung phía trên.</p></div></section>';
        return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Danh sách (' + data.sponsors.length + ')</div>' +
            '<div class="d-grid gap-2">' + data.sponsors.map(function (s) {
                var logo = Ui.safeUrl(s.logoUrl);
                return '<div class="member-card">' +
                    (logo ? '<img class="sponsor-logo sponsor-tier-' + esc(s.tier.toLowerCase()) + '" src="' + esc(logo) + '" alt="" />' : '<div class="av">' + esc(s.name.charAt(0)) + '</div>') +
                    '<div class="grow min-w-0"><div class="nm text-truncate">' + esc(s.name) + '</div>' +
                    '<div class="rl">Hạng ' + esc(s.tierLabel) + ' · Thứ tự ' + s.displayOrder + (s.showWhenCancelled ? '' : ' · Ẩn khi giải hủy') + '</div>' +
                    (s.websiteUrl ? '<a class="hint text-truncate d-block" href="' + esc(Ui.safeUrl(s.websiteUrl) || '#') + '" target="_blank" rel="noopener noreferrer">' + esc(s.websiteUrl) + '</a>' : '') + '</div>' +
                    '<div class="d-flex gap-2"><button type="button" class="btn btn-ghost btn-sm" data-edit="' + s.tournamentSponsorId + '">Sửa</button>' +
                    '<button type="button" class="btn btn-ghost btn-sm text-danger-etoms" data-delete="' + s.tournamentSponsorId + '">Gỡ</button></div></div>';
            }).join('') + '</div>' +
            '<a class="btn btn-ghost mt-3" href="/bracket/view.html?id=' + tournamentId + '">Xem trang giải công khai</a></div></section>';
    }

    function render() {
        document.getElementById('spn-root').innerHTML = '<div class="d-flex flex-column gap-3">' + formHtml() + listHtml() + '</div>';
        var form = document.getElementById('spn-form');
        var cancel = document.getElementById('sp-cancel');
        if (cancel) cancel.addEventListener('click', function () { editingId = null; render(); });
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash); Ui.clearFieldErrors(form);
            var errors = validate(form);
            if (Object.keys(errors).length) { Ui.applyFieldErrors(form, errors); return; }
            var fd = new FormData();
            fd.append('Name', form.Name.value.trim());
            fd.append('Tier', form.Tier.value);
            fd.append('WebsiteUrl', form.WebsiteUrl.value.trim());
            fd.append('ContactInfo', form.ContactInfo.value.trim());
            if (form.DisplayOrder.value.trim()) fd.append('DisplayOrder', form.DisplayOrder.value.trim());
            fd.append('ShowWhenCancelled', form.ShowWhenCancelled.checked ? 'true' : 'false');
            if (form.RemoveLogo) fd.append('RemoveLogo', form.RemoveLogo.checked ? 'true' : 'false');
            if (form.logo.files[0]) fd.append('logo', form.logo.files[0]);
            var btn = form.querySelector('[type=submit]');
            btn.disabled = true;
            try {
                var url = '/api/tournaments/' + tournamentId + '/sponsors' + (editingId ? '/' + editingId : '');
                var res = editingId ? await Api.putForm(url, fd) : await Api.postForm(url, fd);
                editingId = null;
                await load();
                Ui.showAlert(flash, 'success', res.message);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.message || 'Không lưu được nhà tài trợ.');
            } finally { btn.disabled = false; }
        });
    }

    async function load() {
        try {
            data = (await Api.get('/api/tournaments/' + tournamentId + '/sponsors')).data;
        } catch (err) {
            showEmpty(err.status === 404 ? 'Không tìm thấy giải đấu' : 'Không mở được danh sách nhà tài trợ', err.message);
            return;
        }
        document.getElementById('page-title').textContent = 'Nhà tài trợ · ' + data.tournamentName;
        render();
    }

    document.addEventListener('DOMContentLoaded', async function () {
        flash = document.getElementById('flash');
        var me = await Ui.requireAuth(['ORGANIZER']);
        if (!me) return;
        tournamentId = Ui.qsInt('id');
        if (!tournamentId || tournamentId < 1) { showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.'); return; }
        document.getElementById('spn-root').addEventListener('click', async function (e) {
            var edit = e.target.closest('[data-edit]');
            if (edit) {
                editingId = parseInt(edit.dataset.edit, 10);
                render();
                document.getElementById('spn-form').scrollIntoView({ behavior: 'smooth', block: 'start' });
                return;
            }
            var del = e.target.closest('[data-delete]');
            if (!del) return;
            var id = parseInt(del.dataset.delete, 10);
            var s = data.sponsors.find(function (x) { return x.tournamentSponsorId === id; });
            if (!await window.etomsConfirm('Gỡ nhà tài trợ "' + (s ? s.name : '') + '" khỏi giải? Logo sẽ không còn hiện trên trang giải.')) return;
            try {
                var res = await Api.del('/api/tournaments/' + tournamentId + '/sponsors/' + id);
                if (editingId === id) editingId = null;
                await load();
                Ui.showAlert(flash, 'success', res.message);
            } catch (err) { Ui.showAlert(flash, 'danger', err.message || 'Không gỡ được nhà tài trợ.'); }
        });
        await load();
    });
})();
