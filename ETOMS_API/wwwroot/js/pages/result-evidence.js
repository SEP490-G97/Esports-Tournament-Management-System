(function () {
    var matchId = null;

    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDateTime(iso) {
        var d = new Date(iso);
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + pad2(d.getHours()) + ':' + pad2(d.getMinutes());
    }

    function render(ev, canGoSubmit) {
        var root = document.getElementById('evidence-root');

        var actions = '<a class="btn btn-ghost" href="/result/history.html?matchId=' + matchId + '">Xem lịch sử phán quyết</a>';
        if (canGoSubmit) actions += '<a class="btn btn-primary" href="/result/submit.html?matchId=' + matchId + '">Nộp báo cáo của đội tôi</a>';

        var body;
        if (!ev.isVisible) {
            body = '<div class="card-etoms"><div class="empty"><h3>Minh chứng chưa công khai</h3><p>' + Ui.escapeHtml(ev.hiddenReason) + '</p></div></div>';
        } else {
            var cardsHtml = '<div class="row g-3">';
            ev.reports.forEach(function (r) {
                var filesHtml;
                if (!r.files.length) {
                    filesHtml = '<p class="text-muted-etoms">Không có file minh chứng.</p>';
                } else {
                    filesHtml = '<div style="display:grid;gap:10px">';
                    r.files.forEach(function (f) {
                        filesHtml += '<div style="background:var(--panel-2);border:1px solid var(--border);border-radius:10px;padding:10px 14px;display:flex;justify-content:space-between;align-items:center;gap:10px">' +
                            '<span class="badge badge-soft">' + Ui.escapeHtml(f.fileType) + '</span>' +
                            '<a href="' + Ui.escapeHtml(f.fileUrl) + '" target="_blank" rel="noopener" style="color:var(--cyan);font-weight:600;word-break:break-all">Mở file minh chứng ↗</a></div>';
                        if (f.fileType === 'IMAGE') {
                            filesHtml += '<img src="' + Ui.escapeHtml(f.fileUrl) + '" alt="Minh chứng ' + Ui.escapeHtml(r.teamName) + '" style="width:100%;border-radius:10px;border:1px solid var(--border)" />';
                        }
                    });
                    filesHtml += '</div>';
                }
                cardsHtml += '<div class="col-12 col-md-6"><div class="card-etoms h-100"><div class="body">' +
                    '<div class="section-title mt-0">' + Ui.escapeHtml(r.teamName) + '</div>' +
                    '<div class="section-sub">Nộp lúc ' + fmtDateTime(r.submittedAt) + ' · Tỉ số báo cáo: ' + r.scoreA + ' - ' + r.scoreB + '</div>' +
                    filesHtml + '</div></div></div>';
            });
            cardsHtml += '</div>';

            var flagBlock = '';
            if (ev.canFlag) {
                flagBlock =
                    '<div class="card-etoms form-narrow mt-4"><div class="body">' +
                    '<div class="section-title mt-0">Cắm cờ nghi vấn</div>' +
                    '<div class="section-sub">Nếu bạn cho rằng kết quả này sai (kể cả khi hai đội đã đồng thuận), hãy tố cáo kèm lý do cụ thể.</div>' +
                    '<form id="flag-form">' +
                    '<div class="field"><label>Loại nghi vấn <span class="req">*</span></label>' +
                    '<select name="Category" class="select" required>' +
                    '<option value="WRONG_SCORE">Sai tỉ số</option><option value="CHEATING">Gian lận</option>' +
                    '<option value="INELIGIBLE_PLAYER">Cầu thủ không đủ điều kiện</option><option value="OTHER">Khác</option></select></div>' +
                    '<div class="field"><label>Lý do cụ thể <span class="req">*</span></label>' +
                    '<textarea name="Note" class="input" rows="3" minlength="10" maxlength="1000" required placeholder="Nêu rõ căn cứ, không được bỏ trống"></textarea></div>' +
                    '<div class="btn-row"><button type="submit" class="btn btn-ghost text-danger-etoms">Cắm cờ nghi vấn</button></div>' +
                    '</form></div></div>';
            }

            body = cardsHtml + flagBlock;
        }

        root.innerHTML =
            '<div class="eyebrow">Kết quả &amp; Khiếu nại · RES-04</div>' +
            '<div class="page-head"><h1>Minh chứng trận đấu</h1><p><strong class="text-ink">' + Ui.escapeHtml(ev.teamAName) + '</strong> vs <strong class="text-ink">' + Ui.escapeHtml(ev.teamBName) + '</strong></p></div>' +
            '<div class="btn-row mb-4">' + actions + '</div>' + body;

        var flagForm = document.getElementById('flag-form');
        if (flagForm) {
            flagForm.addEventListener('submit', async function (e) {
                e.preventDefault();
                var ok = await window.etomsConfirm('Cắm cờ nghi vấn trận này? Trận sẽ bị đóng băng chờ Trọng tài xem xét.');
                if (!ok) return;
                try {
                    var res = await Api.post('/api/matches/' + matchId + '/result/flag', {
                        Category: flagForm.querySelector('[name="Category"]').value,
                        Note: flagForm.querySelector('[name="Note"]').value
                    });
                    Ui.setFlash(res.message, 'success');
                    location.reload();
                } catch (err) {
                    Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
                }
            });
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        Ui.consumeFlash(document.getElementById('flash'));
        matchId = Ui.qsInt('matchId');
        var root = document.getElementById('evidence-root');
        if (!matchId) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã trận</h3></div></div>'; return; }

        var me = { isAuthenticated: false };
        try { me = await Api.get('/api/dev-auth/me'); } catch (e) {}

        var canGoSubmit = false;
        if (me.isAuthenticated) {
            try {
                var page = await Api.get('/api/matches/' + matchId + '/result/submission');
                canGoSubmit = !!page.canSubmit;
            } catch (e) { canGoSubmit = false; }
        }

        try {
            var ev = await Api.get('/api/matches/' + matchId + '/result/evidence');
            render(ev, canGoSubmit);
        } catch (err) {
            if (err.status === 404) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy trận đấu</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    });
})();
