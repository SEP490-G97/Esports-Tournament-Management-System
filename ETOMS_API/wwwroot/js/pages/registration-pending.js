(function () {
    var tournamentId = null;

    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDateTime(iso) {
        var d = new Date(iso);
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + pad2(d.getHours()) + ':' + pad2(d.getMinutes());
    }

    function render(list) {
        var root = document.getElementById('pending-root');
        if (!list.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không có đơn chờ duyệt</h3></div></div>';
            return;
        }
        var html = '<div class="d-grid gap-3">';
        list.forEach(function (r) {
            var submitted = r.submittedAt ? '· nộp ' + fmtDateTime(r.submittedAt) : '';
            html +=
                '<div class="card-etoms"><div class="body d-flex flex-wrap align-items-center gap-3" style="padding:16px 20px">' +
                '<div style="flex:1;min-width:200px"><div style="font-weight:700">' + Ui.escapeHtml(r.teamName) + ' <span style="color:var(--muted);font-weight:500">[' + Ui.escapeHtml(r.teamTag) + ']</span></div>' +
                '<div style="font-size:.84rem;color:var(--muted)">Đội trưởng: ' + Ui.escapeHtml(r.captainName) + ' ' + submitted + '</div></div>' +
                '<div class="d-flex flex-wrap gap-2">' +
                '<button type="button" class="btn btn-primary" style="padding:8px 14px" data-act="approve" data-id="' + r.registrationId + '">Duyệt</button>' +
                '<form class="reject-form d-flex align-items-center gap-2" data-id="' + r.registrationId + '">' +
                '<input type="text" name="reason" class="input" style="width:180px" placeholder="Lý do từ chối" />' +
                '<button type="submit" class="btn btn-ghost" style="padding:8px 14px;color:var(--danger-ink)">Từ chối</button></form>' +
                '</div></div></div>';
        });
        html += '</div>';
        root.innerHTML = html;

        root.querySelectorAll('[data-act="approve"]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                await doAction('/api/registrations/' + btn.dataset.id + '/approve', {});
            });
        });
        root.querySelectorAll('.reject-form').forEach(function (form) {
            form.addEventListener('submit', async function (e) {
                e.preventDefault();
                var reason = form.querySelector('[name="reason"]').value.trim();
                if (!reason) return;
                await doAction('/api/registrations/' + form.dataset.id + '/reject', { Reason: reason });
            });
        });
    }

    async function doAction(url, body) {
        var flash = document.getElementById('flash');
        try {
            var res = await Api.post(url, body);
            Ui.showAlert(flash, 'success', res.message);
            await load();
        } catch (err) {
            Ui.showAlert(flash, 'danger', err.message);
        }
    }

    async function load() {
        var root = document.getElementById('pending-root');
        try {
            var list = await Api.get('/api/registrations/pending?tournamentId=' + tournamentId);
            render(list);
        } catch (err) {
            if (err.status === 403) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Bạn không có quyền duyệt đơn giải này</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        tournamentId = Ui.qsInt('tournamentId');
        document.getElementById('back-link').href = '/bracket/manage.html?id=' + tournamentId;
        if (!tournamentId) { document.getElementById('pending-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã giải</h3></div></div>'; return; }
        var me = await Ui.requireAuth();
        if (!me) return;
        await load();
    });
})();
