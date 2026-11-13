(function () {
    var tournamentId = null;

    function showEmpty(title, message) {
        document.getElementById('clone-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    // Ô datetime-local trả giờ máy người dùng; gửi lên server dạng UTC.
    function toUtc(id) {
        var value = document.getElementById(id).value;
        return value ? new Date(value).toISOString() : null;
    }

    function toLocalInput(date) {
        var pad = function (n) { return String(n).padStart(2, '0'); };
        return date.getFullYear() + '-' + pad(date.getMonth() + 1) + '-' + pad(date.getDate()) +
            'T' + pad(date.getHours()) + ':' + pad(date.getMinutes());
    }

    function openPicker(input) {
        try { input.showPicker(); } catch (e) { /* trình duyệt cũ vẫn dùng được biểu tượng lịch */ }
    }

    // Ngày giờ chỉ chọn trên lịch: bấm vào ô là mở, không gõ tay. Tab vẫn đi qua được các ô.
    function wireDatePickers() {
        var ids = ['RegOpenAt', 'RegCloseAt', 'StartAt'];
        var now = toLocalInput(new Date());

        ids.forEach(function (id, index) {
            var input = document.getElementById(id);
            input.min = now;
            input.addEventListener('click', function () { openPicker(input); });
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Tab') return;
                e.preventDefault();
                if (e.key === 'Enter' || e.key === ' ') openPicker(input);
            });

            // Mốc sau không được chọn sớm hơn mốc trước.
            var next = ids[index + 1];
            if (next) {
                input.addEventListener('change', function () {
                    document.getElementById(next).min = input.value || now;
                });
            }
        });
    }

    function wireSubmit() {
        var form = document.getElementById('clone-form');
        var flash = document.getElementById('flash');
        var btn = document.getElementById('clone-btn');

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);

            btn.disabled = true;
            try {
                var res = await Api.post('/api/tournaments/' + tournamentId + '/clone', {
                    name: document.getElementById('Name').value,
                    regOpenAt: toUtc('RegOpenAt'),
                    regCloseAt: toUtc('RegCloseAt'),
                    startAt: toUtc('StartAt')
                });
                Ui.setFlash(res.message, 'success');
                location.href = '/tournament/manage.html?id=' + res.data;
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.status === 400 && Object.keys(err.fieldErrors).length ? 'Vui lòng kiểm tra lại thông tin.' : err.message);
                btn.disabled = false;
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        tournamentId = Ui.qsInt('id');
        if (tournamentId === null) {
            showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.');
            return;
        }

        try {
            var source = await Api.get('/api/tournaments/' + tournamentId + '/publish-status');
            document.getElementById('page-title').textContent = 'Nhân bản giải ' + source.tournamentName;
            document.getElementById('Name').value = (source.tournamentName + ' (bản sao)').substring(0, 200);
            document.getElementById('back-link').href = '/tournament/manage.html?id=' + tournamentId;
            document.getElementById('clone-root').innerHTML = '';
            document.getElementById('clone-card').hidden = false;
            wireDatePickers();
            wireSubmit();
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể nhân bản giải này', 'Chỉ Ban tổ chức của giải được nhân bản.');
            else showEmpty('Không tải được thông tin giải', err.message);
        }
    });
})();
