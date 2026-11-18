(function () {
    var EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    var accountId;
    var organizationId;
    var requiredDocumentTypes = [];
    var uploadedDocumentTypes = [];
    var flash = document.getElementById('register-flash');

    function showFailure(error, fallback) {
        Ui.showAlert(flash, 'danger', (error && error.message) || fallback);
    }

    function setRegistrationStep(step) {
        document.querySelectorAll('[data-registration-step]').forEach(function (item) {
            var itemStep = Number(item.dataset.registrationStep);
            item.classList.toggle('active', itemStep === step);
            item.classList.toggle('complete', itemStep < step);
            if (itemStep === step) item.setAttribute('aria-current', 'step');
            else item.removeAttribute('aria-current');
        });
    }

    // Chuyển qua lại 2 bước. Sau khi đã tạo hồ sơ: bước 1 thành "sửa thông tin" (ẩn danh sách người cùng lập — lời mời
    // đã gửi, mời thêm ở trang đơn vị), nút gửi đổi thành "Lưu thay đổi".
    function showStep(step) {
        document.getElementById('register-panel').hidden = step !== 1;
        document.getElementById('document-panel').hidden = step !== 2;
        setRegistrationStep(step);
        if (step === 1 && organizationId) {
            var founders = document.querySelector('.org-founders');
            if (founders) founders.hidden = true;
            var submit = document.querySelector('#organization-form [type="submit"]');
            if (submit) submit.textContent = 'Lưu thay đổi và quay lại bước 2';
        }
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function renderRequiredDocuments() {
        var root = document.getElementById('required-documents');
        root.innerHTML = requiredDocumentTypes.map(function (documentType) {
            var uploaded = uploadedDocumentTypes.includes(documentType);
            return '<div class="feature-list-item"><div class="item-title">' + Ui.escapeHtml(documentType) +
                '</div><span class="badge ' + (uploaded ? 'badge-active' : 'badge-gold') + '">' +
                (uploaded ? 'Đã tải' : 'Cần bổ sung') + '</span></div>';
        }).join('');
    }

    function wireDocumentUpload() {
        var form = document.getElementById('document-form');
        form.addEventListener('submit', async function (event) {
            event.preventDefault();
            Ui.clearFieldErrors(form);
            var input = document.getElementById('organization-document');
            if (!input.files[0]) {
                Ui.applyFieldErrors(form, { file: 'Chọn tệp cần tải lên.' });
                return;
            }
            var documentType = document.getElementById('document-type').value;
            if (!documentType) {
                Ui.applyFieldErrors(form, { documentType: 'Nhập tên loại giấy tờ.' });
                document.getElementById('document-type').focus();
                return;
            }
            var file = input.files[0];
            var allowedExtensions = ['.jpg', '.jpeg', '.png', '.pdf'];
            var extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
            if (!allowedExtensions.includes(extension)) {
                Ui.applyFieldErrors(form, { file: 'Chỉ chấp nhận tệp JPG, PNG hoặc PDF.' });
                return;
            }
            if (file.size === 0 || file.size > 10 * 1024 * 1024) {
                Ui.applyFieldErrors(form, { file: 'Tệp phải có dung lượng lớn hơn 0 và không vượt quá 10 MB.' });
                return;
            }
            var body = new FormData();
            body.append('documentType', documentType);
            body.append('file', file);
            var submitButton = form.querySelector('[type="submit"]');
            submitButton.disabled = true;
            document.getElementById('document-upload-status').textContent = 'Đang tải tệp lên...';
            try {
                var response = await Api.postForm('/api/organizations/' + organizationId + '/documents', body);
                var doc = response.data;
                if (!uploadedDocumentTypes.includes(doc.documentType)) {
                    uploadedDocumentTypes.push(doc.documentType);
                }
                renderRequiredDocuments();
                var root = document.getElementById('uploaded-documents');
                root.insertAdjacentHTML('afterbegin', '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(doc.documentType) + '</div><div class="item-sub">' + Ui.escapeHtml(doc.originalFileName) + '</div></div><span class="badge badge-active">Đã tải</span></div>');
                setRegistrationStep(2);
                var missingCount = requiredDocumentTypes.filter(function (type) {
                    return !uploadedDocumentTypes.includes(type);
                }).length;
                document.getElementById('document-upload-status').textContent = missingCount
                    ? 'Đã tải giấy tờ. Còn ' + missingCount + ' loại giấy tờ bắt buộc cần bổ sung.'
                    : 'Đã đủ giấy tờ bắt buộc. Hồ sơ vẫn đang chờ quản trị viên xét duyệt.';
                Ui.showAlert(flash, 'success', response.message || 'Đã tải giấy tờ lên.');
                form.reset();
            } catch (error) {
                Ui.applyFieldErrors(form, error.fieldErrors);
                showFailure(error, 'Không thể tải giấy tờ lên.');
                document.getElementById('document-upload-status').textContent = '';
            } finally {
                submitButton.disabled = false;
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;
        // Đơn vị tổ chức gồm các tài khoản Ban tổ chức — tuyển thủ/Admin không lập đơn vị (máy chủ cũng chặn).
        if (me.role !== 'ORGANIZER') {
            var main = document.querySelector('main');
            main.innerHTML = '<div class="card-etoms mt-4"><div class="empty"><h3>Chỉ tài khoản Ban tổ chức được lập đơn vị</h3>' +
                '<p>Muốn tổ chức giải, hãy gửi yêu cầu làm Ban tổ chức ở Hồ sơ cá nhân. Admin sẽ cấp tài khoản Ban tổ chức riêng cho bạn.</p>' +
                '<a class="btn btn-primary mt-2" href="/profile/index.html#organizer-request">Xin làm Ban tổ chức</a></div></div>';
            return;
        }
        accountId = Number(me.userId);
        wireDocumentUpload();
        document.getElementById('back-to-info').addEventListener('click', function () { showStep(1); });
        // Bấm vào bước trên thanh tiến trình (sau khi đã tạo hồ sơ) cũng chuyển được.
        document.querySelectorAll('[data-registration-step]').forEach(function (item) {
            item.addEventListener('click', function () { if (organizationId) showStep(Number(item.dataset.registrationStep)); });
        });
        // "+ Thêm người": thêm ô email Ban tổ chức cùng lập (tối đa 10).
        document.getElementById('add-founder').addEventListener('click', function () {
            var list = document.getElementById('founder-list');
            if (list.querySelectorAll('input').length >= 10) return;
            var input = document.createElement('input');
            input.name = 'MemberEmail'; input.type = 'email'; input.maxLength = 256; input.className = 'input';
            input.placeholder = 'Email Ban tổ chức thứ ' + (list.querySelectorAll('input').length + 2);
            input.autocomplete = 'off';
            list.appendChild(input);
            input.focus();
        });
        document.getElementById('organization-form').addEventListener('submit', async function (event) {
            event.preventDefault();
            var form = event.currentTarget;
            Ui.clearFieldErrors(form);
            var data = new FormData(form);
            var errors = {};
            if (!String(data.get('Name') || '').trim()) errors.Name = 'Nhập tên hiển thị của đơn vị.';
            // Tối thiểu 3 Ban tổ chức tính cả người lập => ít nhất 2 email khác nhau (máy chủ kiểm lại tài khoản).
            var memberEmails = data.getAll('MemberEmail').map(function (v) { return String(v || '').trim().toLowerCase(); })
                .filter(function (v, i, a) { return v && a.indexOf(v) === i; });
            // Đã tạo hồ sơ (quay lại sửa): lời mời đã gửi, không kiểm danh sách người cùng lập nữa.
            if (!organizationId && memberEmails.length < 2) errors.MemberEmails = 'Nhập email tài khoản Ban tổ chức của ít nhất 2 người cùng lập (đơn vị cần tối thiểu 3 Ban tổ chức).';
            else if (!organizationId && memberEmails.some(function (v) { return !EMAIL_RE.test(v); })) errors.MemberEmails = 'Kiểm tra lại định dạng email.';
            var phone = String(data.get('ContactPhone') || '').trim();
            if (phone && !/^0\d{9}$/.test(phone)) errors.ContactPhone = 'Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0.';
            ['ContactEmail', 'WebsiteUrl', 'LogoUrl'].forEach(function (name) {
                var input = form.elements[name];
                if (input.value && !input.validity.valid) errors[name] = 'Kiểm tra lại định dạng thông tin này.';
            });
            if (Object.keys(errors).length) {
                Ui.applyFieldErrors(form, errors);
                var firstInvalid = form.querySelector('[data-field-error]:not(:empty)');
                var invalidField = firstInvalid && firstInvalid.parentElement.querySelector('input, select, textarea');
                if (invalidField) invalidField.focus();
                return;
            }
            var body = {
                Name: data.get('Name'),
                MemberEmails: memberEmails,
                ParentOrganization: data.get('ParentOrganization') || null,
                ContactEmail: data.get('ContactEmail') || null,
                ContactPhone: data.get('ContactPhone') || null,
                WebsiteUrl: data.get('WebsiteUrl') || null,
                LogoUrl: data.get('LogoUrl') || null,
                Description: data.get('Description') || null
            };
            var submitButton = form.querySelector('[type="submit"]');
            submitButton.disabled = true;
            if (organizationId) {
                // Quay lại sửa thông tin hồ sơ đang chờ duyệt rồi về bước 2.
                try {
                    var updated = await Api.put('/api/organizations/' + organizationId, body);
                    showStep(2);
                    Ui.showAlert(flash, 'success', updated.message || 'Đã cập nhật thông tin đơn vị.');
                } catch (error) {
                    Ui.applyFieldErrors(form, error.fieldErrors);
                    showFailure(error, 'Không thể cập nhật thông tin đơn vị.');
                } finally { submitButton.disabled = false; }
                return;
            }
            try {
                var response = await Api.post('/api/organizations', body);
                organizationId = Number(response.data.organizationId);
                requiredDocumentTypes = response.data.requiredDocumentTypes || [];
                uploadedDocumentTypes = [];
                document.getElementById('document-type').innerHTML = '<option value="">Chọn loại giấy tờ</option>' +
                    requiredDocumentTypes.map(function (type) {
                        return '<option value="' + Ui.escapeHtml(type) + '">' + Ui.escapeHtml(type) + '</option>';
                    }).join('');
                renderRequiredDocuments();
                document.getElementById('register-panel').hidden = true;
                document.getElementById('document-panel').hidden = false;
                setRegistrationStep(2);
                document.getElementById('document-type').focus();
                Ui.showAlert(flash, 'success', response.message || 'Đã tạo hồ sơ tổ chức. Hồ sơ đang chờ xét duyệt.');
            } catch (error) {
                Ui.applyFieldErrors(form, error.fieldErrors);
                showFailure(error, 'Không thể đăng ký đơn vị.');
                submitButton.disabled = false;
            }
        });
    });
})();
