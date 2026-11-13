(function () {
    var matchId = null;

    function render(page) {
        var root = document.getElementById('submit-root');

        if (!page.canSubmit) {
            root.innerHTML =
                '<div class="eyebrow">Kết quả &amp; Khiếu nại</div>' +
                '<div class="page-head"><h1>Báo cáo tỉ số mù</h1>' +
                '<p>' + Ui.escapeHtml(page.tournamentName) + ' · Bo' + page.bestOf + ' · <strong style="color:var(--ink)">' + Ui.escapeHtml(page.myTeamName) + '</strong> vs ' + Ui.escapeHtml(page.opponentTeamName) + '</p></div>' +
                '<div class="card-etoms"><div class="empty"><h3>Không thể nộp báo cáo</h3><p>' + Ui.escapeHtml(page.cannotSubmitReason) + '</p>' +
                '<a class="btn btn-ghost" href="/result/evidence.html?matchId=' + page.matchId + '">Xem minh chứng trận này</a></div></div>';
            return;
        }

        root.innerHTML =
            '<div class="eyebrow">Kết quả &amp; Khiếu nại</div>' +
            '<div class="page-head"><h1>Báo cáo tỉ số mù</h1>' +
            '<p>' + Ui.escapeHtml(page.tournamentName) + ' · Bo' + page.bestOf + ' · <strong style="color:var(--ink)">' + Ui.escapeHtml(page.myTeamName) + '</strong> vs ' + Ui.escapeHtml(page.opponentTeamName) + '</p></div>' +
            '<div class="dev-banner">Đây là báo cáo <strong>mù</strong> (Blind Dual-Reporting): bạn sẽ KHÔNG thấy tỉ số, minh chứng hay việc đội kia đã nộp hay chưa ' +
            'cho tới khi cả hai đội cùng nộp xong. Báo cáo sau khi xác nhận <strong>không thể sửa hoặc rút lại</strong>.</div>' +
            '<div id="flash"></div>' +
            '<div class="card-etoms form-narrow"><div class="body">' +
            '<form id="submit-form" novalidate>' +
            '<div class="field"><label>Tỉ số đội <strong style="color:var(--cyan)">' + Ui.escapeHtml(page.myTeamName) + '</strong> (đội bạn) <span class="req">*</span></label>' +
            '<input name="MyScore" type="number" min="0" max="50" class="input" required />' +
            '<span class="field-error" data-field-error="MyScore"></span></div>' +
            '<div class="field"><label>Tỉ số đội đối thủ <span class="req">*</span></label>' +
            '<input name="OpponentScore" type="number" min="0" max="50" class="input" required />' +
            '<span class="field-error" data-field-error="OpponentScore"></span></div>' +
            '<div class="field"><label>Minh chứng — ảnh, video hoặc file replay <span class="req">*</span></label>' +
            '<input type="file" name="evidenceFiles" class="input" multiple accept=".jpg,.jpeg,.png,.webp,.mp4,.webm,.mov,.rofl,.dem" required />' +
            '<div class="hint">Ảnh tối đa 8MB, video tối đa 100MB, file replay tối đa 30MB. Có thể chọn nhiều file.</div>' +
            '<span class="field-error" data-field-error="evidenceFiles"></span></div>' +
            '<div class="field" style="margin-bottom:8px"><label>Tỉ số từng ván (tùy chọn, dùng cho Bo3/Bo5)</label>' +
            '<input name="PerGameScores" class="input" placeholder="Ví dụ: 1-0, 0-1, 1-0" maxlength="200" /></div>' +
            '<div class="alert" style="background:rgba(246,167,35,.1);border:1px solid rgba(246,167,35,.3);color:var(--warn);margin-top:18px">Nộp kết quả? Báo cáo đã nộp không thể sửa hoặc rút lại.</div>' +
            '<div class="btn-row"><button type="submit" class="btn btn-primary">Xác nhận nộp báo cáo</button>' +
            '<a href="/bracket/view.html?id=' + page.tournamentId + '" class="btn btn-ghost">Hủy</a></div>' +
            '</form></div></div>';

        var form = document.getElementById('submit-form');
        var flash = document.getElementById('flash');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);
            var fd = new FormData();
            fd.append('MyScore', form.querySelector('[name="MyScore"]').value);
            fd.append('OpponentScore', form.querySelector('[name="OpponentScore"]').value);
            fd.append('PerGameScores', form.querySelector('[name="PerGameScores"]').value);
            Array.from(form.querySelector('[name="evidenceFiles"]').files).forEach(function (f) { fd.append('evidenceFiles', f); });
            try {
                var res = await Api.postForm('/api/matches/' + matchId + '/result/submission', fd);
                Ui.setFlash(res.message, 'success');
                location.href = '/result/evidence.html?matchId=' + matchId;
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.message);
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        matchId = Ui.qsInt('matchId');
        var root = document.getElementById('submit-root');
        if (!matchId) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã trận</h3></div></div>'; return; }
        var me = await Ui.requireAuth();
        if (!me) return;
        try {
            var page = await Api.get('/api/matches/' + matchId + '/result/submission');
            render(page);
        } catch (err) {
            if (err.status === 404) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy trận đấu</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    });
})();
