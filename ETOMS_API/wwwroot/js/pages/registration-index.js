(function () {
    async function load() {
        var root = document.getElementById('reg-root');
        try {
            var list = await Api.get('/api/registrations/mine');
            render(list);
        } catch (err) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    }

    function render(list) {
        var root = document.getElementById('reg-root');
        if (!list.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Chưa có đơn đăng ký nào</h3>' +
                '<p>Nếu bạn là đội trưởng, hãy nộp đơn tham gia một giải đấu.</p>' +
                '<a class="btn btn-primary" href="/registration/create.html">Nộp đơn đăng ký</a></div></div>';
            return;
        }

        var html = '<div class="d-grid gap-3">';
        list.forEach(function (r) {
            var waitlist = (r.status === 'WAITLISTED' && r.waitlistPosition) ? ' #' + r.waitlistPosition : '';
            var rejectBlock = (r.status === 'REJECTED' && r.reviewNote)
                ? '<div class="alert alert-danger mt-3 mb-0">Lý do từ chối: ' + Ui.escapeHtml(r.reviewNote) + '</div>' : '';
            var proofForm = r.canUploadProof
                ? '<form class="proof-form d-flex flex-wrap align-items-center gap-2 mt-3 pt-3" data-id="' + r.registrationId + '" style="border-top:1px solid var(--border-soft)">' +
                  '<input type="file" name="proofFile" accept=".jpg,.jpeg,.png,.webp" class="input" style="max-width:280px" required />' +
                  '<input type="text" name="note" class="input" style="max-width:220px" placeholder="Ghi chú (không bắt buộc)" />' +
                  '<button type="submit" class="btn btn-ghost">Tải minh chứng lệ phí</button></form>'
                : '';
            html +=
                '<div class="card-etoms"><div class="body" style="padding:20px 22px">' +
                '<div class="d-flex flex-wrap justify-content-between align-items-start gap-3">' +
                '<div><div style="font-weight:800;font-size:1.05rem">' + Ui.escapeHtml(r.tournamentName) + '</div>' +
                '<div style="color:var(--muted);font-size:.86rem;margin-top:2px">' + Ui.escapeHtml(r.gameTitle) + '</div></div>' +
                '<div class="d-flex flex-wrap gap-2">' +
                '<span class="badge ' + Ui.escapeHtml(r.statusClass) + '">' + Ui.escapeHtml(r.statusLabel) + waitlist + '</span>' +
                '<span class="badge badge-soft">Lệ phí: ' + Ui.escapeHtml(r.paymentStatusLabel) + '</span>' +
                '</div></div>' + rejectBlock + proofForm + '</div></div>';
        });
        html += '</div>';
        root.innerHTML = html;

        root.querySelectorAll('.proof-form').forEach(function (form) {
            form.addEventListener('submit', async function (e) {
                e.preventDefault();
                var flash = document.getElementById('flash');
                Ui.clearAlert(flash);
                var fd = new FormData();
                var file = form.querySelector('[name="proofFile"]').files[0];
                if (file) fd.append('proofFile', file);
                fd.append('note', form.querySelector('[name="note"]').value);
                try {
                    var res = await Api.postForm('/api/registrations/' + form.dataset.id + '/proof', fd);
                    Ui.showAlert(flash, 'success', res.message);
                    await load();
                } catch (err) {
                    Ui.showAlert(flash, 'danger', err.message);
                }
            });
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        Ui.consumeFlash(document.getElementById('flash'));
        var me = await Ui.requireAuth();
        if (!me) return;
        await load();
    });
})();
