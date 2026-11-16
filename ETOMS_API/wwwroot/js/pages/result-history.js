(function () {
    var matchId = null;

    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDateTime(iso) {
        var d = new Date(iso);
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + pad2(d.getHours()) + ':' + pad2(d.getMinutes());
    }

    function render(trail) {
        var root = document.getElementById('history-root');

        var eventsHtml;
        if (!trail.events.length) {
            eventsHtml = '<div class="card-etoms"><div class="empty"><h3>Chưa có sự kiện nào</h3><p>Trận đấu chưa có báo cáo hoặc phán quyết nào được ghi nhận.</p></div></div>';
        } else {
            eventsHtml = '<div class="d-grid gap-3">';
            trail.events.forEach(function (e) {
                eventsHtml += '<div class="card-etoms"><div class="body" style="padding:16px 20px;border-left:3px solid var(--primary);border-radius:0 14px 14px 0">' +
                    '<div class="d-flex flex-wrap justify-content-between align-items-start gap-2">' +
                    '<div class="fw-bold">' + Ui.escapeHtml(e.title) + '</div>' +
                    '<span class="text-muted-2 text-small">' + fmtDateTime(e.at) + '</span></div>' +
                    (e.detail ? '<div style="color:var(--muted);font-size:.9rem;margin-top:6px">' + Ui.escapeHtml(e.detail) + '</div>' : '') +
                    (e.actorName ? '<div style="color:var(--muted-2);font-size:.82rem;margin-top:4px">Bởi: ' + Ui.escapeHtml(e.actorName) + '</div>' : '') +
                    '</div></div>';
            });
            eventsHtml += '</div>';
        }

        var appealBlock = '';
        if (trail.appealableDisputeId != null) {
            appealBlock =
                '<div class="card-etoms form-narrow mt-4"><div class="body">' +
                '<div class="section-title mt-0">Khiếu nại phán quyết</div>' +
                '<div class="section-sub">Đội trưởng của 1 trong 2 đội được khiếu nại đúng 1 lần cho phán quyết trên.</div>' +
                '<form id="appeal-form">' +
                '<div class="field"><label>Căn cứ khiếu nại <span class="req">*</span></label>' +
                '<textarea name="Reason" class="input" rows="4" minlength="10" maxlength="1000" required placeholder="Nêu rõ vì sao bạn cho rằng phán quyết chưa đúng"></textarea></div>' +
                '<div class="btn-row"><button type="submit" class="btn btn-ghost text-gold">Gửi khiếu nại</button></div>' +
                '</form></div></div>';
        }

        root.innerHTML =
            '<div class="eyebrow">Kết quả &amp; Khiếu nại · Lịch sử phán quyết</div>' +
            '<div class="page-head"><h1>Lịch sử phán quyết</h1><p><strong class="text-ink">' + Ui.escapeHtml(trail.teamAName) + '</strong> vs <strong class="text-ink">' + Ui.escapeHtml(trail.teamBName) + '</strong></p></div>' +
            '<div class="btn-row mb-4"><a class="btn btn-ghost" href="/result/evidence.html?matchId=' + matchId + '">Xem minh chứng</a></div>' +
            eventsHtml + appealBlock;

        var appealForm = document.getElementById('appeal-form');
        if (appealForm) {
            appealForm.addEventListener('submit', async function (e) {
                e.preventDefault();
                var flash = document.getElementById('flash');
                Ui.clearAlert(flash);
                try {
                    var res = await Api.post('/api/disputes/' + trail.appealableDisputeId + '/appeal', { Reason: appealForm.querySelector('[name="Reason"]').value });
                    Ui.setFlash(res.message, 'success');
                    location.reload();
                } catch (err) {
                    Ui.showAlert(flash, 'danger', err.message);
                }
            });
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        Ui.consumeFlash(document.getElementById('flash'));
        matchId = Ui.qsInt('matchId');
        var root = document.getElementById('history-root');
        if (!matchId) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã trận</h3></div></div>'; return; }
        try {
            var trail = await Api.get('/api/matches/' + matchId + '/result/history');
            render(trail);
        } catch (err) {
            if (err.status === 404) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy trận đấu</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    });
})();
