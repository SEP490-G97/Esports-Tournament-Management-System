(function () {
    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['PLAYER']);
        if (!me) return;

        var code = Ui.qs('code');
        if (code) document.getElementById('Code').value = code.toUpperCase();

        var form = document.getElementById('join-form');
        var flash = document.getElementById('flash');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);
            try {
                var res = await Api.post('/api/teams/join', { Code: form.querySelector('#Code').value });
                Ui.setFlash(res.message, 'success');
                location.href = '/team/details.html?id=' + res.data;
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                if (!err.fieldErrors || !err.fieldErrors.Code) Ui.showAlert(flash, 'danger', err.message);
            }
        });
    });
})();
