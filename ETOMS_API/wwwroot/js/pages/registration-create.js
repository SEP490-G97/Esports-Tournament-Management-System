(function () {
    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDate(iso) {
        var d = new Date(iso);
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + '/' + d.getFullYear();
    }

    function render(tours) {
        var root = document.getElementById('create-root');
        if (!tours.length) {
            root.innerHTML = '<div class="empty"><h3>Không có giải nào đang mở đăng ký</h3></div>';
            return;
        }

        var options = '<option value="">— Chọn giải đấu —</option>';
        tours.forEach(function (t) {
            options += '<option value="' + t.tournamentId + '">' + Ui.escapeHtml(t.name) + '</option>';
        });

        var infoRows = tours.map(function (t) {
            return '<div class="d-flex justify-content-between" style="font-size:.85rem;color:var(--muted);padding:10px 14px;background:var(--panel-2);border-radius:10px">' +
                '<span>' + Ui.escapeHtml(t.name) + ' (' + Ui.escapeHtml(t.gameTitle) + ')</span>' +
                '<span>' + t.approvedCount + '/' + t.maxTeams + ' đội · hạn ' + fmtDate(t.regCloseAt) + '</span></div>';
        }).join('');

        root.innerHTML =
            '<form id="create-form" novalidate>' +
            '<div class="field"><label for="TournamentId">Giải đấu <span class="req">*</span></label>' +
            '<select id="TournamentId" name="TournamentId" class="select">' + options + '</select>' +
            '<span class="field-error" data-field-error="TournamentId"></span></div>' +
            '<div class="d-grid gap-2 my-3">' + infoRows + '</div>' +
            '<div class="btn-row"><button type="submit" class="btn btn-primary">Nộp đơn</button><a href="/registration/index.html" class="btn btn-ghost">Hủy</a></div>' +
            '</form>';

        var form = document.getElementById('create-form');
        var flash = document.getElementById('flash');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);
            var tid = form.querySelector('#TournamentId').value;
            try {
                var res = await Api.post('/api/registrations', { TournamentId: tid ? parseInt(tid, 10) : null });
                Ui.setFlash(res.message, 'success');
                location.href = '/registration/index.html';
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.message);
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;
        try {
            var tours = await Api.get('/api/registrations/open-tournaments');
            render(tours);
        } catch (e) {
            render([]);
        }
    });
})();
