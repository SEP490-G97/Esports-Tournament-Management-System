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
            var code = form.querySelector('#Code').value.trim().toUpperCase();
            if (!code) { Ui.applyFieldErrors(form, { Code: 'Vui lòng nhập mã mời.' }); return; }
            if (!/^[A-Z0-9]{4,40}$/.test(code)) { Ui.applyFieldErrors(form, { Code: 'Mã mời chỉ gồm chữ cái không dấu và chữ số.' }); return; }
            var submit = form.querySelector('button[type="submit"]');
            submit.disabled = true;
            try {
                var res = await Api.post('/api/teams/join', { Code: code });
                Ui.setFlash(res.message, 'success');
                location.href = '/team/details.html?id=' + res.data;
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                if (!err.fieldErrors || !err.fieldErrors.Code) Ui.showAlert(flash, 'danger', err.message);
                submit.disabled = false;
            }
        });
    });
})();
