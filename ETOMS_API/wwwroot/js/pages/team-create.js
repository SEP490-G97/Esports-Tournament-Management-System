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

            // Cùng luật với CreateTeamRequest + ImageUploader phía máy chủ (giới hạn độ dài theo cột DB).
            var name = form.querySelector('#Name').value.trim();
            var tag = form.querySelector('#Tag').value.trim();
            var logo = form.querySelector('#logoFile').files[0];
            var errors = {};
            if (name.length < 2 || name.length > 150) errors.Name = 'Tên đội phải từ 2 đến 150 ký tự.';
            if (!/^[A-Za-z0-9]{2,10}$/.test(tag)) errors.Tag = 'Thẻ đội gồm 2–10 chữ cái không dấu hoặc chữ số, không khoảng trắng.';
            if (!form.querySelector('#GameId').value) errors.GameId = 'Vui lòng chọn tựa game chủ đạo.';
            if (logo && !/\.(jpe?g|png|webp)$/i.test(logo.name)) errors.logoFile = 'Logo phải có định dạng JPG, PNG hoặc WEBP.';
            else if (logo && logo.size > 2 * 1024 * 1024) errors.logoFile = 'Logo không được vượt quá 2MB.';
            if (Object.keys(errors).length) {
                Ui.applyFieldErrors(form, errors);
                Ui.showAlert(flash, 'danger', 'Vui lòng sửa các ô được đánh dấu.');
                return;
            }

            var fd = new FormData();
            fd.append('Name', form.querySelector('#Name').value);
            fd.append('Tag', form.querySelector('#Tag').value);
            fd.append('GameId', form.querySelector('#GameId').value);
            var file = form.querySelector('#logoFile').files[0];
            if (file) fd.append('logoFile', file);

            try {
                var res = await Api.postForm('/api/teams', fd);
                Ui.setFlash(res.message, 'success');
                location.href = '/team/dashboard.html?id=' + res.data;   // trang quản trị: mã mời, roster
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
