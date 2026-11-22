// UC-34 / UC-35 — bảng kỷ luật của giải: tổng điểm phạt từng đội, cấp thẻ phạt, hủy thẻ (ghi dòng hủy, không xóa),
// đặt ngưỡng loại tự động (trước khi giải bắt đầu). Máy chủ kiểm lại toàn bộ quyền và dữ liệu.
(function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var tournamentId, data = null, flash;
    var SEVERITIES = [['WARNING', 'Cảnh cáo', 1], ['MINOR', 'Lỗi nhẹ', 2], ['MAJOR', 'Lỗi nặng', 3]];

    function fmt(iso) {
        if (!iso) return '';
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? '' : d.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    }

    function showEmpty(title, text) {
        document.getElementById('pen-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>' + esc(title) + '</h3><p>' + esc(text || '') + '</p></div></div>';
    }

    function thresholdHtml() {
        var th = data.penaltyThreshold;
        var text = th ? 'Đội đạt <strong>' + th + ' điểm phạt</strong> sẽ bị tước quyền thi đấu tự động.' : 'Giải chưa đặt ngưỡng loại tự động.';
        if (!data.canSetThreshold)
            return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Ngưỡng điểm phạt</div><p class="mb-0">' + text +
                (data.isOrganizer ? ' <span class="hint">Ngưỡng chỉ đặt được trước khi giải bắt đầu.</span>' : '') + '</p></div></section>';
        return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Ngưỡng điểm phạt</div><p>' + text + '</p>' +
            '<form id="th-form" class="d-flex flex-wrap gap-2 align-items-end" novalidate>' +
            '<div class="field mb-0"><label for="th-value">Ngưỡng (để trống nếu không áp dụng)</label>' +
            '<input id="th-value" name="Threshold" type="number" min="1" max="100" class="input" style="max-width:180px" value="' + (th || '') + '" />' +
            '<span class="field-error" data-field-error="Threshold"></span></div>' +
            '<button type="submit" class="btn btn-primary">Lưu ngưỡng</button></form></div></section>';
    }

    function issueHtml() {
        var teams = data.teams.filter(function (t) { return t.canIssue; });
        if (!data.canIssue)
            return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Cấp thẻ phạt</div><p class="mb-0 hint">Chỉ cấp thẻ phạt khi giải đang diễn ra (trạng thái hiện tại: ' + esc(data.tournamentStatus) + ').</p></div></section>';
        if (!teams.length)
            return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Cấp thẻ phạt</div><p class="mb-0 hint">Không có đội nào bạn được cấp thẻ phạt (trọng tài chỉ phạt đội trong trận mình được phân công).</p></div></section>';
        return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Cấp thẻ phạt</div>' +
            '<form id="issue-form" novalidate>' +
            '<div class="row g-3"><div class="col-12 col-md-6"><div class="field mb-0"><label for="pf-team">Đội <span class="req">*</span></label>' +
            '<select id="pf-team" name="TeamId" class="select">' + teams.map(function (t) {
                return '<option value="' + t.teamId + '">' + esc(t.teamName) + ' (đang có ' + t.activePoints + ' điểm)</option>';
            }).join('') + '</select><span class="field-error" data-field-error="TeamId"></span></div></div>' +
            '<div class="col-6 col-md-3"><div class="field mb-0"><label for="pf-sev">Mức phạt <span class="req">*</span></label>' +
            '<select id="pf-sev" name="Severity" class="select">' + SEVERITIES.map(function (s) {
                return '<option value="' + s[0] + '" data-points="' + s[2] + '">' + s[1] + '</option>';
            }).join('') + '</select><span class="field-error" data-field-error="Severity"></span></div></div>' +
            '<div class="col-6 col-md-3"><div class="field mb-0"><label for="pf-points">Điểm phạt <span class="req">*</span></label>' +
            '<input id="pf-points" name="Points" type="number" min="0" max="100" class="input" value="1" />' +
            '<span class="field-error" data-field-error="Points"></span></div></div></div>' +
            '<div class="field mt-3"><label for="pf-reason">Lý do <span class="req">*</span></label>' +
            '<textarea id="pf-reason" name="Reason" class="input" rows="3" maxlength="1000" placeholder="Nêu điều thể lệ bị vi phạm và diễn biến."></textarea>' +
            '<span class="field-error" data-field-error="Reason"></span></div>' +
            '<button type="submit" class="btn btn-primary">Cấp thẻ phạt</button></form></div></section>';
    }

    function teamsHtml() {
        if (!data.teams.length) return '';
        var th = data.penaltyThreshold;
        return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Tổng điểm phạt theo đội</div>' +
            '<div class="table-responsive"><table class="data-table"><thead><tr><th>Đội</th><th>Trạng thái</th><th class="text-end">Điểm phạt</th></tr></thead><tbody>' +
            data.teams.map(function (t) {
                return '<tr><td><a href="/team/details.html?id=' + t.teamId + '">' + esc(t.teamName) + '</a></td><td>' + esc(t.registrationStatusLabel) + '</td>' +
                    '<td class="text-end"><strong>' + t.activePoints + '</strong>' + (th ? ' / ' + th : '') + '</td></tr>';
            }).join('') + '</tbody></table></div></div></section>';
    }

    function penaltiesHtml() {
        if (!data.penalties.length)
            return '<section class="card-etoms"><div class="empty"><h3>Chưa có thẻ phạt</h3><p>Thẻ phạt đã cấp và đã hủy của giải sẽ hiện ở đây.</p></div></section>';
        return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Hồ sơ kỷ luật của giải (' + data.penalties.length + ')</div>' +
            '<div class="d-grid gap-2">' + data.penalties.map(function (p) {
                var head = esc(p.severityLabel) + (p.points ? ' · ' + p.points + ' điểm' : '') + ' — ' + esc(p.teamName);
                var sub = esc(p.statusLabel) + ' · ' + esc(p.issuedByName) + ' · ' + fmt(p.issuedAt) + (p.matchId ? ' · <a href="/match/detail.html?id=' + p.matchId + '">Trận #' + p.matchId + '</a>' : '');
                var link = p.cancelsPenaltyId ? '<div class="hint">Hủy thẻ #' + p.cancelsPenaltyId + '</div>'
                    : p.cancelledByPenaltyId ? '<div class="hint">Đã hủy bởi dòng #' + p.cancelledByPenaltyId + '</div>' : '';
                return '<div class="member-card' + (p.status === 'CANCELLED' ? ' opacity-75' : '') + '" id="pen-' + p.penaltyId + '">' +
                    '<div class="grow min-w-0"><div class="nm">#' + p.penaltyId + ' · ' + head + '</div><div class="rl">' + sub + '</div>' +
                    '<div>' + esc(p.reason) + '</div>' + link +
                    (p.canCancel ? '<form class="d-none mt-2" data-cancel-form="' + p.penaltyId + '" novalidate>' +
                        '<div class="field mb-2"><label for="cf-' + p.penaltyId + '">Lý do hủy thẻ <span class="req">*</span></label>' +
                        '<input id="cf-' + p.penaltyId + '" name="Reason" class="input" maxlength="500" placeholder="Ví dụ: cấp nhầm đội" />' +
                        '<span class="field-error" data-field-error="Reason"></span></div>' +
                        '<button type="submit" class="btn btn-primary btn-sm">Xác nhận hủy thẻ</button></form>' : '') + '</div>' +
                    (p.canCancel ? '<button type="button" class="btn btn-ghost btn-sm" data-cancel="' + p.penaltyId + '">Hủy thẻ</button>' : '') + '</div>';
            }).join('') + '</div></div></section>';
    }

    function render() {
        document.getElementById('pen-root').innerHTML = '<div class="d-flex flex-column gap-3">' +
            thresholdHtml() + issueHtml() + teamsHtml() + penaltiesHtml() + '</div>';
        wire();
    }

    function wire() {
        var th = document.getElementById('th-form');
        if (th) th.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash); Ui.clearFieldErrors(th);
            var raw = th.Threshold.value.trim();
            var v = raw === '' ? null : parseInt(raw, 10);
            if (raw !== '' && (isNaN(v) || v < 1 || v > 100)) { Ui.applyFieldErrors(th, { Threshold: 'Ngưỡng điểm phạt từ 1 đến 100, hoặc để trống nếu không áp dụng.' }); return; }
            try {
                var res = await Api.put('/api/tournaments/' + tournamentId + '/penalty-threshold', { Threshold: v });
                await load(); Ui.showAlert(flash, 'success', res.message);
            } catch (err) { Ui.applyFieldErrors(th, err.fieldErrors); Ui.showAlert(flash, 'danger', err.message || 'Không lưu được ngưỡng.'); }
        });

        var form = document.getElementById('issue-form');
        if (!form) return;
        form.Severity.addEventListener('change', function () {
            form.Points.value = form.Severity.selectedOptions[0].dataset.points;
        });
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash); Ui.clearFieldErrors(form);
            var reason = form.Reason.value.trim();
            var points = parseInt(form.Points.value, 10);
            var errors = {};
            if (!reason) errors.Reason = 'Vui lòng nhập lý do cấp thẻ phạt.';
            if (isNaN(points) || points < 0 || points > 100) errors.Points = 'Điểm phạt từ 0 đến 100.';
            if (Object.keys(errors).length) { Ui.applyFieldErrors(form, errors); return; }
            var team = form.TeamId.selectedOptions[0].textContent;
            if (!await window.etomsConfirm('Cấp thẻ phạt cho ' + team + '? Thẻ phạt không xóa được, chỉ hủy bằng một dòng hủy có lý do.')) return;
            var btn = form.querySelector('[type=submit]');
            btn.disabled = true;
            try {
                var res = await Api.post('/api/tournaments/' + tournamentId + '/penalties', {
                    TeamId: parseInt(form.TeamId.value, 10), Severity: form.Severity.value, Points: points, Reason: reason
                });
                await load(); Ui.showAlert(flash, 'success', res.message);
            } catch (err) { Ui.applyFieldErrors(form, err.fieldErrors); Ui.showAlert(flash, 'danger', err.message || 'Không cấp được thẻ phạt.'); }
            finally { btn.disabled = false; }
        });
    }

    async function load() {
        try {
            data = (await Api.get('/api/tournaments/' + tournamentId + '/discipline')).data;
        } catch (err) {
            showEmpty(err.status === 404 ? 'Không tìm thấy giải đấu' : 'Không mở được bảng kỷ luật', err.message);
            return;
        }
        document.getElementById('page-title').textContent = 'Kỷ luật · ' + data.tournamentName;
        render();
    }

    document.addEventListener('DOMContentLoaded', async function () {
        flash = document.getElementById('flash');
        var me = await Ui.requireAuth(['ORGANIZER']);
        if (!me) return;
        tournamentId = Ui.qsInt('id');
        if (!tournamentId || tournamentId < 1) { showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.'); return; }

        var root = document.getElementById('pen-root');
        root.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-cancel]');
            if (!btn) return;
            var f = root.querySelector('[data-cancel-form="' + btn.dataset.cancel + '"]');
            if (f) { f.classList.toggle('d-none'); if (!f.classList.contains('d-none')) f.Reason.focus(); }
        });
        // UC-34 A2: hủy = ghi dòng hủy liên kết kèm lý do; thẻ gốc vẫn hiện trong hồ sơ.
        root.addEventListener('submit', async function (e) {
            var f = e.target.closest('[data-cancel-form]');
            if (!f) return;
            e.preventDefault();
            Ui.clearAlert(flash); Ui.clearFieldErrors(f);
            var reason = f.Reason.value.trim();
            if (!reason) { Ui.applyFieldErrors(f, { Reason: 'Vui lòng nhập lý do hủy thẻ phạt.' }); return; }
            try {
                var res = await Api.post('/api/tournaments/' + tournamentId + '/penalties/' + f.dataset.cancelForm + '/cancel', { Reason: reason });
                await load(); Ui.showAlert(flash, 'success', res.message);
            } catch (err) { Ui.applyFieldErrors(f, err.fieldErrors); Ui.showAlert(flash, 'danger', err.message || 'Không hủy được thẻ phạt.'); }
        });
        await load();
    });
})();
