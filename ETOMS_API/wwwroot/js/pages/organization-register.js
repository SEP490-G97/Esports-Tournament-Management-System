(function () {
    var accountId;
    var organizationId;
    var flash = document.getElementById('register-flash');

    function showFailure(error, fallback) {
        Ui.showAlert(flash, 'danger', (error && error.message) || fallback);
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
            var body = new FormData();
            body.append('documentType', document.getElementById('document-type').value);
            body.append('file', input.files[0]);
            try {
                var response = await Api.postForm('/api/organizations/' + organizationId + '/documents', body);
                var doc = response.data;
                var root = document.getElementById('uploaded-documents');
                root.insertAdjacentHTML('afterbegin', '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(doc.documentType) + '</div><div class="item-sub">' + Ui.escapeHtml(doc.originalFileName) + '</div></div><span class="badge badge-active">Đã tải</span></div>');
                Ui.showAlert(flash, 'success', response.message || 'Đã tải giấy tờ lên.');
                form.reset();
            } catch (error) {
                Ui.applyFieldErrors(form, error.fieldErrors);
                showFailure(error, 'Không thể tải giấy tờ lên.');
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
            try {
                var response = await Api.post('/api/organizations', body);
                organizationId = Number(response.data.organizationId);
                document.getElementById('register-panel').hidden = true;
                document.getElementById('document-panel').hidden = false;
                document.getElementById('organization-link').href = '/organization/details.html?id=' + organizationId;
                Ui.showAlert(flash, 'success', response.message || 'Đã tạo hồ sơ. Hãy bổ sung giấy tờ.');
            } catch (error) {
                Ui.applyFieldErrors(form, error.fieldErrors);
                showFailure(error, 'Không thể đăng ký đơn vị.');
            }
        });
    });
})();
