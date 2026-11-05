(function () {
    function render(accounts) {
        var root = document.getElementById('account-list');
        if (!accounts || !accounts.length) {
            root.innerHTML = '<div class="empty"><h3>Chưa có tài khoản demo</h3></div>';
            return;
        }
        var html = '<div class="pick-list">';
        accounts.forEach(function (u) {
            var initial = u.fullName && u.fullName.length > 0 ? u.fullName.substring(0, 1) : '?';
            html +=
                '<button type="button" class="pick-item" data-account-id="' + u.accountId + '" style="width:100%;text-align:left;cursor:pointer">' +
                '<span class="av">' + Ui.escapeHtml(initial) + '</span>' +
                '<span class="grow"><div style="font-weight:600">' + Ui.escapeHtml(u.fullName) + '</div><div class="em">' + Ui.escapeHtml(u.email) + '</div></span>' +
                '<span class="badge-soft">Chọn</span>' +
                '</button>';
        });
        html += '</div>';
        root.innerHTML = html;

        root.querySelectorAll('[data-account-id]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                try {
                    await Api.post('/api/dev-auth/login', { AccountId: parseInt(btn.dataset.accountId, 10) });
                    var returnUrl = Ui.qs('returnUrl');
                    location.href = (returnUrl && returnUrl.startsWith('/')) ? returnUrl : '/';
                } catch (err) {
                    root.insertAdjacentHTML('beforebegin', '<div class="alert alert-danger">' + Ui.escapeHtml(err.message) + '</div>');
                }
            });
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        try {
            var accounts = await Api.get('/api/dev-auth/accounts');
            render(accounts);
        } catch (e) {
            render([]);
        }
    });
})();
