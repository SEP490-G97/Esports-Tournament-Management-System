(function () {
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
        var me;
        try {
            me = await Api.get('/api/dev-auth/me');
        } catch (error) {
            showFailure(error, 'Không thể xác định tài khoản hiện tại.');
            document.getElementById('register-panel').hidden = true;
            return;
        }
        if (!me.isAuthenticated || !Number(me.userId)) {
            location.href = '/dev-auth/index.html?returnUrl=' + encodeURIComponent(location.pathname + location.search);
            return;
        }
        accountId = Number(me.userId);
        wireDocumentUpload();
        document.getElementById('organization-form').addEventListener('submit', async function (event) {
            event.preventDefault();
            var form = event.currentTarget;
            Ui.clearFieldErrors(form);
            var data = new FormData(form);
            var errors = {};
            if (!String(data.get('Name') || '').trim()) errors.Name = 'Nhập tên hiển thị của đơn vị.';
            if (!data.get('OrganizationType')) errors.OrganizationType = 'Chọn loại hình đơn vị.';
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
                OrganizationType: data.get('OrganizationType'),
                ParentOrganization: data.get('ParentOrganization') || null,
                ContactEmail: data.get('ContactEmail') || null,
                ContactPhone: data.get('ContactPhone') || null,
                WebsiteUrl: data.get('WebsiteUrl') || null,
                LogoUrl: data.get('LogoUrl') || null,
                Description: data.get('Description') || null,
                CreatedByAccountId: accountId
            };
            var submitButton = form.querySelector('[type="submit"]');
            submitButton.disabled = true;
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
