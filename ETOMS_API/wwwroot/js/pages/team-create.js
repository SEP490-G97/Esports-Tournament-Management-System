(function () {
    async function populateGames() {
        var select = document.getElementById('GameId');
        try {
            var games = await Api.get('/api/teams/games');
            games.forEach(function (g) {
                var opt = document.createElement('option');
                opt.value = g.gameId;
                opt.textContent = g.title;
                select.appendChild(opt);
            });
        } catch (e) {}
    }

    function wireLogoPreview() {
        var input = document.getElementById('logoFile');
        var img = document.getElementById('logoPreview');
        input.addEventListener('change', function () {
            if (input.files && input.files[0]) {
                img.src = URL.createObjectURL(input.files[0]);
                img.style.display = 'block';
            } else {
                img.style.display = 'none';
            }
        });
    }

    function wireSubmit() {
        var form = document.getElementById('create-form');
        var flash = document.getElementById('flash');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);

            var fd = new FormData();
            fd.append('Name', form.querySelector('#Name').value);
            fd.append('Tag', form.querySelector('#Tag').value);
            fd.append('GameId', form.querySelector('#GameId').value);
            var file = form.querySelector('#logoFile').files[0];
            if (file) fd.append('logoFile', file);

            try {
                var res = await Api.postForm('/api/teams', fd);
                Ui.setFlash(res.message, 'success');
                location.href = '/team/details.html?id=' + res.data;
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.message);
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['PLAYER']);
        if (!me) return;
        await populateGames();
        wireLogoPreview();
        wireSubmit();
    });
})();
