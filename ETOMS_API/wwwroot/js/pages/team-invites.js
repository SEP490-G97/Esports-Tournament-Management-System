(function () {
    var teamId = null;

    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDateTime(iso) {
        var d = new Date(iso);
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + pad2(d.getHours()) + ':' + pad2(d.getMinutes());
    }

    function render(invite) {
        var root = document.getElementById('invite-root');
        var link = invite && invite.isUsable ? (location.origin + '/team/join.html?code=' + invite.code) : null;

        var currentBlock;
        if (invite && invite.isUsable) {
            currentBlock =
                '<div class="section-sub" style="margin-bottom:10px">Mã mời hiện tại</div>' +
                '<div class="d-flex flex-wrap align-items-center gap-3 mb-2">' +
                '<div style="font-family:monospace;font-size:1.6rem;font-weight:800;letter-spacing:3px;color:var(--cyan)">' + Ui.escapeHtml(invite.code) + '</div>' +
                '<span class="badge badge-active">Đang hiệu lực</span>' +
                '<span style="color:var(--muted);font-size:.86rem">' + (invite.expiresAt ? 'Hết hạn: ' + fmtDateTime(invite.expiresAt) : 'Vô thời hạn') + '</span>' +
                '</div>' +
                '<div class="btn-row">' +
                '<button type="button" class="btn btn-ghost" id="copy-link-btn">Sao chép link mời</button>' +
                '<button type="button" class="btn btn-ghost" style="color:var(--danger-ink)" id="revoke-btn">Thu hồi mã</button>' +
                '</div>';
        } else {
            currentBlock = '<div class="section-sub">Đội chưa có mã mời đang hiệu lực. Tạo một mã bên dưới.</div>';
        }

        root.innerHTML =
            '<div class="eyebrow">Quản trị đội · Mã mời</div>' +
            '<div class="page-head"><h1>Mã mời thành viên</h1><p>Mỗi đội có một mã mời đang hiệu lực. Tạo mã mới sẽ thay mã cũ. Có thể đặt thời hạn.</p></div>' +
            '<div class="card-etoms form-narrow"><div class="body">' + currentBlock + '</div></div>' +
            '<div class="card-etoms form-narrow" style="margin-top:20px"><div class="body">' +
            '<div class="section-sub" style="margin-bottom:12px">' + (invite && invite.isUsable ? 'Tạo mã mới (thay mã hiện tại)' : 'Tạo mã mời') + '</div>' +
            '<form id="generate-form">' +
            '<div class="field"><label for="ExpiresHours">Thời hạn (giờ)</label>' +
            '<input id="ExpiresHours" name="ExpiresHours" class="input" type="number" min="1" max="8760" placeholder="Để trống = vô thời hạn" />' +
            '<span class="field-error" data-field-error="ExpiresHours"></span></div>' +
            '<div class="btn-row"><button type="submit" class="btn btn-primary">Tạo mã mời</button></div>' +
            '</form></div></div>' +
            '<div class="btn-row" style="margin-top:26px"><a class="btn btn-ghost" href="/team/dashboard.html?id=' + teamId + '">← Về trang quản trị</a></div>';

        var copyBtn = document.getElementById('copy-link-btn');
        if (copyBtn) {
            copyBtn.addEventListener('click', function () {
                navigator.clipboard.writeText(link).then(function () {
                    var old = copyBtn.innerText;
                    copyBtn.innerText = 'Đã sao chép!';
                    setTimeout(function () { copyBtn.innerText = old; }, 1500);
                });
            });
        }
        var revokeBtn = document.getElementById('revoke-btn');
        if (revokeBtn) {
            revokeBtn.addEventListener('click', async function () {
                if (!(await window.etomsConfirm('Thu hồi mã mời hiện tại?'))) return;
                try {
                    var res = await Api.del('/api/teams/' + teamId + '/invite');
                    Ui.showAlert(document.getElementById('flash'), 'success', res.message);
                    await load();
                } catch (err) {
                    Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
                }
            });
        }

        var form = document.getElementById('generate-form');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearFieldErrors(form);
            var flash = document.getElementById('flash');
            Ui.clearAlert(flash);
            var hoursVal = form.querySelector('#ExpiresHours').value;
            var body = { ExpiresHours: hoursVal ? parseInt(hoursVal, 10) : null };
            try {
                var res = await Api.post('/api/teams/' + teamId + '/invite', body);
                Ui.showAlert(flash, 'success', res.message);
                await load();
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.message);
            }
        });
    }

    async function load() {
        try {
            var invite = await Api.get('/api/teams/' + teamId + '/invite');
            render(invite);
        } catch (err) {
            var root = document.getElementById('invite-root');
            if (err.status === 403) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Bạn không phải đội trưởng đội này</h3></div></div>';
            else if (err.status === 404) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy đội</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        teamId = Ui.qsInt('id');
        if (!teamId) { document.getElementById('invite-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã đội</h3></div></div>'; return; }
        var me = await Ui.requireAuth();
        if (!me) return;
        await load();
    });
})();
