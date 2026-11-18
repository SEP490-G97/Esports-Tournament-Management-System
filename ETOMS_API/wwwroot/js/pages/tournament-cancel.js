(function () {
    var GROUP_BADGES = {
        TO_REFUND: ['badge-danger', 'Cần hoàn'],
        TO_VERIFY: ['badge-gold', 'Cần đối soát trước'],
        REFUNDED: ['badge-active', 'Đã hoàn']
    };
    var tournamentId = null;
    var board = null;

    function flash() { return document.getElementById('flash'); }
    function esc(s) { return Ui.escapeHtml(s == null ? '' : String(s)); }

    function showEmpty(title, message) {
        document.getElementById('cancel-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>' + esc(title) + '</h3>' +
            '<p>' + esc(message) + '</p></div></div>';
    }

    function money(n) { return Number(n).toLocaleString('vi-VN') + 'đ'; }

    function formatTime(value) {
        if (!value) return '';
        var d = new Date(/Z$/.test(value) ? value : value + 'Z');
        return d.toLocaleString('vi-VN');
    }

    function refundHtml(b) {
        if (!b.refundTeams.length) {
            return b.entryFee > 0 ? '<div class="hint">Chưa đội nào nộp lệ phí nên không có khoản cần hoàn.</div>' : '';
        }
        var rows = b.refundTeams.map(function (r) {
            var badge = GROUP_BADGES[r.group] || ['', r.group];
            return '<tr data-refund="' + r.registrationId + '">' +
                '<td>' + esc(r.teamName) + '</td>' +
                '<td>' + esc(r.captainName) + '<div class="rl">' + esc(r.captainEmail) + '</div></td>' +
                '<td>' + esc(r.registrationStatusLabel) + '</td>' +
                '<td>' + esc(r.paymentLabel) + '</td>' +
                '<td class="num">' + money(r.amount) + '</td>' +
                '<td><span class="badge ' + badge[0] + '">' + badge[1] + '</span></td></tr>';
        }).join('');
        return '<div class="refund-wrap"><table class="refund" id="refund-table"><thead><tr>' +
            '<th>Đội</th><th>Đội trưởng</th><th>Đơn</th><th>Lệ phí</th><th class="text-end">Số tiền</th><th>Việc cần làm</th>' +
            '</tr></thead><tbody>' + rows + '</tbody></table></div>' +
            '<div class="hint mt-2">ETOMS không chuyển tiền. Ban tổ chức hoàn ngoài hệ thống rồi ghi nhận ở trang đối soát lệ phí; ' +
            'đội đã gửi biên lai nhưng chưa đối soát cần được xác nhận trước khi ghi nhận hoàn.</div>' +
            '<div class="btn-row">' +
            '<button type="button" class="btn btn-ghost" id="export-refund">Xuất danh sách (CSV)</button>' +
            '<a class="btn btn-ghost" href="/registration/review.html?tournamentId=' + tournamentId + '">Mở trang đối soát lệ phí</a></div>';
    }

    function formHtml(b) {
        var refundLine = b.toRefundCount || b.toVerifyCount
            ? '<li>' + b.toRefundCount + ' đội đã nộp lệ phí cần được hoàn' + (b.toVerifyCount ? ', ' + b.toVerifyCount + ' đội đã gửi biên lai chưa đối soát' : '') + '. Danh sách ở bên dưới.</li>'
            : '';
        return '<div class="card-etoms danger-card"><div class="body">' +
            '<div class="section-title mt-0">Hủy giải</div>' +
            '<ul class="consequences" style="margin:10px 0 16px;padding-left:20px">' +
            '<li>Giải chuyển sang trạng thái <strong>Đã hủy</strong> và không hoàn tác được.</li>' +
            '<li>' + b.captainCount + ' đội trưởng có đơn đăng ký nhận thông báo kèm lý do.</li>' + refundLine +
            '<li>Việc hủy được ghi vào nhật ký giải.</li></ul>' +
            '<form id="cancel-form" novalidate>' +
            '<div class="field"><label for="Reason">Lý do hủy giải <span class="req">*</span></label>' +
            '<textarea id="Reason" class="input" maxlength="500" placeholder="Ví dụ: Không đủ số đội đăng ký tối thiểu."></textarea>' +
            '<div class="hint"><span id="reason-count">0</span>/500 ký tự. Lý do được gửi nguyên văn tới các đội trưởng.</div>' +
            '<span class="field-error" data-field-error="Reason"></span></div>' +
            '<div class="btn-row"><button type="submit" class="btn btn-cancel-tour" id="cancel-btn">Hủy giải</button>' +
            '<a class="btn btn-ghost" href="/tournament/manage.html?id=' + tournamentId + '">Quay lại</a></div>' +
            '</form></div></div>';
    }

    function render(b) {
        board = b;
        document.getElementById('page-title').textContent = 'Hủy giải · ' + b.tournamentName;
        document.getElementById('page-sub').innerHTML = 'Trạng thái: <span class="badge ' + esc(b.statusClass) + '" id="status-badge">' + esc(b.statusLabel) + '</span>';

        var top;
        if (b.isCancelled) {
            top = '<div class="card-etoms"><div class="body" id="cancelled-info">' +
                '<div class="section-title mt-0">Giải đã bị hủy</div>' +
                '<p style="margin:8px 0 0"><strong>Lý do:</strong> ' + esc(b.cancelReason) + '</p>' +
                (b.cancelledAt ? '<div class="hint mt-2">Hủy lúc ' + esc(formatTime(b.cancelledAt)) + (b.cancelledByName ? ' bởi ' + esc(b.cancelledByName) : '') + '.</div>' : '') +
                '</div></div>';
        } else if (!b.canCancel) {
            top = '<div class="card-etoms"><div class="body"><div class="pending-banner" id="blocked-reason">' + esc(b.blockedReason) + '</div>' +
                '<div class="btn-row"><a class="btn btn-ghost" href="/tournament/manage.html?id=' + tournamentId + '">Quản lý giải</a></div></div></div>';
        } else {
            top = formHtml(b);
        }

        var refund = refundHtml(b);
        document.getElementById('cancel-root').innerHTML = top +
            (refund ? '<div class="card-etoms mt-3"><div class="body">' +
                '<div class="section-title mt-0">Đội cần hoàn lệ phí</div>' + refund + '</div></div>' : '');
        wire();
    }

    function csvCell(value) {
        var text = value === null || value === undefined ? '' : String(value);
        return /[",\r\n]/.test(text) ? '"' + text.replace(/"/g, '""') + '"' : text;
    }

    function exportCsv() {
        var labels = { TO_REFUND: 'Cần hoàn', TO_VERIFY: 'Cần đối soát trước', REFUNDED: 'Đã hoàn' };
        var lines = [['Đội', 'Đội trưởng', 'Email', 'Đơn', 'Lệ phí', 'Số tiền', 'Việc cần làm']];
        board.refundTeams.forEach(function (r) {
            lines.push([r.teamName, r.captainName, r.captainEmail, r.registrationStatusLabel, r.paymentLabel, r.amount, labels[r.group] || r.group]);
        });
        var csv = '﻿' + lines.map(function (l) { return l.map(csvCell).join(','); }).join('\r\n');
        var link = document.createElement('a');
        link.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
        link.download = 'hoan-le-phi-giai-' + tournamentId + '.csv';
        document.body.appendChild(link);
        link.click();
        setTimeout(function () { URL.revokeObjectURL(link.href); link.remove(); }, 0);
    }

    function wire() {
        var exportBtn = document.getElementById('export-refund');
        if (exportBtn) exportBtn.addEventListener('click', exportCsv);
        var form = document.getElementById('cancel-form');
        if (!form) return;
        var reason = document.getElementById('Reason');
        reason.addEventListener('input', function () { document.getElementById('reason-count').textContent = reason.value.length; });
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash());
            Ui.clearFieldErrors(form);
            if (!reason.value.trim()) {
                Ui.applyFieldErrors(form, { Reason: 'Vui lòng nhập lý do hủy giải.' });
                reason.focus();
                return;
            }
            var ok = window.etomsConfirm
                ? await window.etomsConfirm('Hủy giải ' + board.tournamentName + '? Việc này không hoàn tác được và ' + board.captainCount + ' đội trưởng sẽ nhận thông báo kèm lý do.')
                : true;
            if (!ok) return;
            var btn = document.getElementById('cancel-btn');
            btn.disabled = true;
            try {
                var res = await Api.post('/api/tournaments/' + tournamentId + '/cancel', { reason: reason.value });
                render(res.data);
                Ui.showAlert(flash(), 'success', res.message);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash(), 'danger', (err.fieldErrors && err.fieldErrors.Reason) || err.message);
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
            var res = await Api.get('/api/tournaments/' + tournamentId + '/cancel');
            render(res.data);
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể hủy giải này', 'Chỉ Ban tổ chức của giải được hủy giải.');
            else showEmpty('Không tải được thông tin hủy giải', err.message);
        }
    });
})();
