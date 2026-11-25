(function () {
    var disputeId = null;

    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDateTime(iso) {
        var d = new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z'); // máy chủ trả giờ UTC không kèm Z
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + pad2(d.getHours()) + ':' + pad2(d.getMinutes());
    }
    function fmtTime(iso) {
        var d = new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z'); // máy chủ trả giờ UTC không kèm Z
        return pad2(d.getHours()) + ':' + pad2(d.getMinutes());
    }
    function reasonLabel(r) {
        return r === 'FLAGGED' ? 'Bị cắm cờ nghi vấn' : r === 'CONFLICTING_REPORT' ? 'Hai báo cáo lệch nhau' : 'Hết hạn báo cáo';
    }
    function statusLabel(s) {
        return s === 'UNDER_REVIEW' ? 'Đang xem xét' : s === 'RESOLVED' ? 'Đã phán quyết' : s === 'CLOSED' ? 'Đã đóng' : 'Mới mở';
    }
    function statusBadgeClass(s) {
        return s === 'UNDER_REVIEW' ? 'badge-active' : s === 'RESOLVED' ? 'badge-gold' : 'badge-soft';
    }

    function render(d) {
        var root = document.getElementById('dispute-root');

        var flaggedBanner = d.openReason === 'FLAGGED'
            ? '<div class="dev-banner">Bị cắm cờ bởi <strong>' + Ui.escapeHtml(d.flaggedByName) + '</strong> — loại: ' + Ui.escapeHtml(d.flagCategory) + '. Lý do: ' + Ui.escapeHtml(d.flagNote) + '</div>'
            : '';

        var reportsHtml = '<div class="row g-3">';
        d.reports.forEach(function (r) {
            var filesHtml = (r.files || []).map(function (f) {
                return '<div class="mt-2"><a href="' + Ui.escapeHtml(f.fileUrl) + '" target="_blank" rel="noopener" class="badge badge-soft text-decoration-none">' + Ui.escapeHtml(f.fileType) + ' — mở file ↗</a></div>';
            }).join('');
            reportsHtml += '<div class="col-12 col-md-6"><div class="card-etoms h-100"><div class="body">' +
                '<div class="fw-800">' + Ui.escapeHtml(r.teamName) + '</div>' +
                '<div style="font-size:1.6rem;font-weight:800;color:var(--cyan);margin:6px 0">' + r.scoreA + ' - ' + r.scoreB + '</div>' +
                '<div class="text-muted-2 text-small">Nộp lúc ' + fmtDateTime(r.submittedAt) + '</div>' +
                filesHtml + '</div></div></div>';
        });
        reportsHtml += '</div>';
        if (d.reports.length < 2) {
            reportsHtml += '<div class="hint" style="margin-top:10px;color:var(--warn)">Chỉ có ' + d.reports.length + '/2 báo cáo — đội còn lại không nộp trước hạn.</div>';
        }

        var chatHtml = '';
        if (d.chatLog && d.chatLog.length) {
            chatHtml = '<div class="section-title">Log chat phòng đấu</div><div class="d-grid gap-2">';
            d.chatLog.forEach(function (c) {
                chatHtml += '<div style="background:var(--panel-2);border-radius:10px;padding:8px 12px;font-size:.88rem">' +
                    '<strong>' + Ui.escapeHtml(c.senderName) + '</strong> <span class="text-muted-2">' + fmtTime(c.sentAt) + '</span>' +
                    '<div>' + Ui.escapeHtml(c.content) + '</div></div>';
            });
            chatHtml += '</div>';
        }

        var resolvedHtml = '';
        if (d.status === 'RESOLVED' || d.status === 'CLOSED') {
            resolvedHtml = '<div class="section-title">Phán quyết đã ghi nhận</div><div class="card-etoms"><div class="body">' +
                '<div style="font-weight:800;font-size:1.3rem">' + d.finalScoreA + ' - ' + d.finalScoreB + '</div>' +
                '<div class="text-muted-etoms mt-2">' + Ui.escapeHtml(d.rulingReason) + '</div>' +
                '<div class="text-muted-2 text-small mt-2">Bởi ' + Ui.escapeHtml(d.resolvedByName) + (d.resolvedAt ? ' lúc ' + fmtDateTime(d.resolvedAt) : '') + '</div>' +
                '</div></div>';
        }

        var ruleFormHtml = '';
        if (d.canRule) {
            ruleFormHtml =
                '<div class="card-etoms form-narrow mt-4"><div class="body">' +
                '<div class="section-title mt-0">Ra phán quyết</div>' +
                '<div class="section-sub">Chọn kết luận và ghi lý do. Đội được xử thắng sẽ tự vào trận sau của nhánh đấu.</div>' +
                '<form id="force-form" novalidate>' +
                '<div class="ruling-options">' +
                '<label class="ruling-option"><input type="radio" name="Outcome" value="SCORE" checked /> <span><b>Chốt tỉ số</b><small>Nhập tỉ số cuối cùng; đội nhiều điểm hơn thắng.</small></span></label>' +
                '<label class="ruling-option"><input type="radio" name="Outcome" value="CHEAT" /> <span><b>Một đội gian lận</b><small>Đội vi phạm bị tước quyền thi đấu (rời giải); đối thủ thắng và đi tiếp.</small></span></label>' +
                '<label class="ruling-option"><input type="radio" name="Outcome" value="VOID" /> <span><b>Hủy kết quả</b><small>Không đội nào thắng (cả hai vi phạm). Sau đó Ban tổ chức xử thắng trắng hoặc cho đấu lại ở trang trận.</small></span></label>' +
                '</div>' +
                '<div class="row g-3 mt-1" data-outcome="SCORE"><div class="col-6"><label>Tỉ số ' + Ui.escapeHtml(d.teamAName) + '</label>' +
                '<input name="ScoreA" type="number" min="0" max="50" class="input" /></div>' +
                '<div class="col-6"><label>Tỉ số ' + Ui.escapeHtml(d.teamBName) + '</label>' +
                '<input name="ScoreB" type="number" min="0" max="50" class="input" /></div>' +
                '<span class="field-error col-12" data-field-error="ScoreA"></span></div>' +
                '<div class="field mt-3" data-outcome="CHEAT" hidden><label>Đội vi phạm</label><select name="CheatTeam" class="select">' +
                '<option value="' + Number(d.teamAId) + '">' + Ui.escapeHtml(d.teamAName) + '</option>' +
                '<option value="' + Number(d.teamBId) + '">' + Ui.escapeHtml(d.teamBName) + '</option></select>' +
                '<span class="field-error" data-field-error="DisqualifyTeamId"></span></div>' +
                '<div class="field"><label>Lý do phán quyết <span class="req">*</span></label>' +
                '<textarea name="Reason" class="input" rows="4" minlength="10" maxlength="1000" required placeholder="Căn cứ vào minh chứng/chat log nào để ra quyết định này"></textarea>' +
                '<span class="field-error" data-field-error="Reason"></span></div>' +
                '<div class="btn-row"><button type="submit" class="btn btn-primary">Ghi nhận phán quyết</button></div>' +
                '</form></div></div>';
        }

        var appealFormHtml = '';
        if (d.appealStatus === 'PENDING') {
            appealFormHtml =
                '<div class="card-etoms form-narrow mt-4"><div class="body">' +
                '<div class="section-title mt-0">Khiếu nại đang chờ xử lý</div>' +
                '<div class="section-sub">' + Ui.escapeHtml(d.appealReason) + '</div>' +
                '<form id="appeal-decision-form">' +
                '<div class="field"><label>Tỉ số mới (nếu chấp nhận khiếu nại)</label><div class="row g-2">' +
                '<div class="col-6"><input name="NewScoreA" type="number" min="0" max="50" class="input" placeholder="' + Ui.escapeHtml(d.teamAName) + '" /></div>' +
                '<div class="col-6"><input name="NewScoreB" type="number" min="0" max="50" class="input" placeholder="' + Ui.escapeHtml(d.teamBName) + '" /></div></div></div>' +
                '<div class="field"><label>Kết luận <span class="req">*</span></label>' +
                '<textarea name="OutcomeNote" class="input" rows="3" minlength="10" maxlength="1000" required></textarea>' +
                '<span class="field-error" data-field-error="OutcomeNote"></span></div>' +
                '<div class="btn-row">' +
                '<button type="button" class="btn btn-primary" data-upheld="true">Chấp nhận khiếu nại</button>' +
                '<button type="button" class="btn btn-ghost" data-upheld="false">Bác khiếu nại</button>' +
                '</div></form></div></div>';
        }

        root.innerHTML =
            '<div class="page-head"><h1>' + Ui.escapeHtml(d.teamAName) + ' vs ' + Ui.escapeHtml(d.teamBName) + '</h1>' +
            '<p>' + reasonLabel(d.openReason) + ' <span class="badge ' + statusBadgeClass(d.status) + ' ms-2">' + statusLabel(d.status) + '</span></p></div>' +
            flaggedBanner +
            '<div class="section-title mt-0">Hai báo cáo</div>' + reportsHtml + chatHtml + resolvedHtml + ruleFormHtml + appealFormHtml +
            '<div class="btn-row mt-4">' +
            '<a class="btn btn-ghost" href="/result/disputes.html">← Về hàng đợi tranh chấp</a>' +
            '<a class="btn btn-ghost" href="/result/history.html?matchId=' + d.matchId + '">Xem lịch sử trận</a>' +
            '</div>';

        var forceForm = document.getElementById('force-form');
        if (forceForm) {
            var outcomeOf = function () { return forceForm.querySelector('[name="Outcome"]:checked').value; };
            forceForm.querySelectorAll('[name="Outcome"]').forEach(function (r) {
                r.addEventListener('change', function () {
                    forceForm.querySelectorAll('[data-outcome]').forEach(function (b) { b.hidden = b.dataset.outcome !== outcomeOf(); });
                });
            });
            forceForm.addEventListener('submit', async function (e) {
                e.preventDefault();
                var flash = document.getElementById('flash');
                Ui.clearAlert(flash);
                Ui.clearFieldErrors(forceForm);
                var outcome = outcomeOf();
                var a = parseInt(forceForm.querySelector('[name="ScoreA"]').value, 10);
                var b = parseInt(forceForm.querySelector('[name="ScoreB"]').value, 10);
                if (outcome === 'SCORE' && (isNaN(a) || isNaN(b) || a === b)) {
                    Ui.applyFieldErrors(forceForm, { ScoreA: isNaN(a) || isNaN(b) ? 'Nhập tỉ số của cả hai đội.' : 'Phải có đội thắng: hai tỉ số không được bằng nhau.' });
                    return;
                }
                if (outcome !== 'SCORE') {
                    var teamName = forceForm.querySelector('[name="CheatTeam"]').selectedOptions[0].text;
                    var ok = await window.etomsConfirm(outcome === 'CHEAT'
                        ? 'Tước quyền thi đấu của ' + teamName + '? Đội rời giải, các trận còn lại của đội bị xử thua; đối thủ trận này thắng và đi tiếp.'
                        : 'Hủy kết quả trận, không đội nào thắng? Nhánh đấu sẽ dừng ở trận này cho tới khi Ban tổ chức xử thắng trắng hoặc cho đấu lại.',
                        { title: 'Xác nhận phán quyết', okText: 'Ghi nhận' });
                    if (!ok) return;
                }
                try {
                    var res = await Api.post('/api/disputes/' + disputeId + '/force-update', {
                        ScoreA: outcome === 'SCORE' ? a : 0,
                        ScoreB: outcome === 'SCORE' ? b : 0,
                        IsVoid: outcome === 'VOID',
                        DisqualifyTeamId: outcome === 'CHEAT' ? parseInt(forceForm.querySelector('[name="CheatTeam"]').value, 10) : null,
                        Reason: forceForm.querySelector('[name="Reason"]').value
                    });
                    Ui.setFlash(res.message, 'success');
                    location.reload();
                } catch (err) {
                    Ui.applyFieldErrors(forceForm, err.fieldErrors);
                    Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
                }
            });
        }

        var appealForm = document.getElementById('appeal-decision-form');
        if (appealForm) {
            appealForm.querySelectorAll('[data-upheld]').forEach(function (btn) {
                btn.addEventListener('click', async function () {
                    var flash = document.getElementById('flash');
                    Ui.clearAlert(flash);
                    Ui.clearFieldErrors(appealForm);
                    var newScoreA = appealForm.querySelector('[name="NewScoreA"]').value;
                    var newScoreB = appealForm.querySelector('[name="NewScoreB"]').value;
                    try {
                        var res = await Api.post('/api/disputes/' + disputeId + '/appeal/decision', {
                            Upheld: btn.dataset.upheld === 'true',
                            OutcomeNote: appealForm.querySelector('[name="OutcomeNote"]').value,
                            NewScoreA: newScoreA ? parseInt(newScoreA, 10) : null,
                            NewScoreB: newScoreB ? parseInt(newScoreB, 10) : null
                        });
                        Ui.setFlash(res.message, 'success');
                        location.reload();
                    } catch (err) {
                        Ui.applyFieldErrors(appealForm, err.fieldErrors);
                        Ui.showAlert(flash, 'danger', err.message);
                    }
                });
            });
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        Ui.consumeFlash(document.getElementById('flash'));
        disputeId = Ui.qsInt('id');
        var root = document.getElementById('dispute-root');
        if (!disputeId) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã tranh chấp</h3></div></div>'; return; }
        var me = await Ui.requireAuth();
        if (!me) return;
        try {
            var d = await Api.get('/api/disputes/' + disputeId);
            render(d);
        } catch (err) {
            if (err.status === 403) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Bạn không phải Trọng tài/Ban tổ chức của tranh chấp này</h3></div></div>';
            else if (err.status === 404) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy tranh chấp</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    });
})();
