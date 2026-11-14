(function () {
    function paymentLabel(status) {
        return {
            UNPAID: 'Chưa nộp',
            PROOF_SUBMITTED: 'Chờ đối soát',
            CONFIRMED: 'Đã xác nhận',
            REFUNDED: 'Đã ghi nhận hoàn'
        }[status] || status;
    }

    function renderRecords(records) {
        if (!records.length) return '<div class="item-sub">Chưa có biên lai hoặc ghi nhận hoàn lệ phí.</div>';
        return records.map(function (record) {
            return '<div class="item-sub">' +
                Ui.escapeHtml(record.recordKind === 'REFUND' ? 'Ghi nhận hoàn lệ phí' : 'Biên lai') +
                (record.declaredAmount !== null
                    ? ' · ' + Number(record.declaredAmount).toLocaleString('vi-VN') + ' đ'
                    : '') +
                (record.downloadUrl ? ' · <a href="' + Ui.escapeHtml(record.downloadUrl) + '" data-private-file="' + Ui.escapeHtml(record.fileName || 'bien-lai') + '">Mở tệp</a>' : '') +
                (record.note ? ' · ' + Ui.escapeHtml(record.note) : '') +
                '</div>';
        }).join('');
    }

    function render(root, items, context) {
        items.forEach(function (item) {
            var card = root.querySelector('[data-registration-id="' + item.registrationId + '"]');
            if (!card) return;
            var actions = card.querySelector('.registration-payment-actions');
            if (item.entryFee <= 0) {
                actions.innerHTML = '<div class="mt-3"><strong>Lệ phí: Miễn phí</strong></div>';
                return;
            }

            var paymentControls = item.paymentStatus !== 'REFUNDED'
                ? '<div class="field mt-3"><label for="payment-note-' + item.registrationId + '">Ghi chú đối soát</label>' +
                    '<input id="payment-note-' + item.registrationId + '" class="input" maxlength="500" />' +
                    '<div class="btn-row mt-2"><button type="button" class="btn btn-ghost" data-payment-status="CONFIRMED" data-registration-id="' +
                    item.registrationId + '">Ghi nhận đã nộp</button><button type="button" class="btn btn-ghost" data-payment-status="UNPAID" data-registration-id="' +
                    item.registrationId + '">Ghi nhận chưa nộp</button></div></div>'
                : '';
            var refundForm = item.canRecordRefund
                ? '<form class="refund-form field mt-3" data-registration-id="' + item.registrationId + '">' +
                    '<label>Số tiền hoàn <input class="input" name="amount" type="number" min="0.01" max="' + Number(item.entryFee) + '" step="0.01" required /></label>' +
                    '<label>Ngày hoàn <input class="input" name="refundDate" type="date" max="' +
                    new Date().toISOString().slice(0, 10) + '" required /></label>' +
                    '<label>Phương thức hoàn <input class="input" name="refundMethod" maxlength="100" required placeholder="Ví dụ: chuyển khoản ngân hàng" /></label>' +
                    '<label>Ghi chú thêm <input class="input" name="note" maxlength="300" /></label>' +
                    '<label>Minh chứng hoàn (nếu có) <input class="input" name="evidence" type="file" accept=".jpg,.jpeg,.png,.pdf" /></label>' +
                    '<button class="btn btn-ghost" type="submit">Ghi nhận trạng thái hoàn</button></form>' +
                    '<p class="field-hint">Chỉ lưu trạng thái và minh chứng; ETOMS không thực hiện giao dịch hoặc chuyển tiền.</p>'
                : '';

            actions.innerHTML =
                '<div class="mt-3"><strong>Lệ phí: ' + Number(item.entryFee).toLocaleString('vi-VN') +
                ' đ · ' + Ui.escapeHtml(paymentLabel(item.paymentStatus)) + '</strong>' +
                renderRecords(item.paymentRecords || []) +
                '<p class="field-hint">ETOMS chỉ lưu biên lai và ghi nhận trạng thái lệ phí; không xử lý thanh toán.</p>' +
                paymentControls + refundForm + '</div>';
        });

        root.querySelectorAll('[data-payment-status]').forEach(function (button) {
            button.addEventListener('click', function () {
                updatePaymentStatus(button, context);
            });
        });
        root.querySelectorAll('.refund-form').forEach(function (form) {
            form.addEventListener('submit', function (event) {
                recordRefund(event, context);
            });
        });
    }

    async function updatePaymentStatus(button, context) {
        var card = button.closest('[data-registration-id]');
        var note = card.querySelector('#payment-note-' + button.dataset.registrationId).value.trim();
        card.querySelectorAll('[data-payment-status]').forEach(function (action) { action.disabled = true; });
        try {
            var result = await Api.put('/api/tournaments/' + context.tournamentId + '/registrations/' +
                button.dataset.registrationId + '/payment-status', {
                status: button.dataset.paymentStatus,
                note: note || null
            });
            Ui.showAlert(context.flash, 'success', result.message || 'Đã cập nhật trạng thái lệ phí.');
            await context.reload();
        } catch (error) {
            Ui.showAlert(card.querySelector('.review-flash'), 'danger', error.message || 'Không thể cập nhật trạng thái.');
            card.querySelectorAll('[data-payment-status]').forEach(function (action) { action.disabled = false; });
        }
    }

    async function recordRefund(event, context) {
        event.preventDefault();
        var form = event.currentTarget;
        var body = new FormData();
        body.append('amount', form.elements.amount.value);
        body.append('refundDate', form.elements.refundDate.value);
        body.append('refundMethod', form.elements.refundMethod.value);
        if (form.elements.note.value.trim()) body.append('note', form.elements.note.value.trim());
        if (form.elements.evidence.files[0]) body.append('evidence', form.elements.evidence.files[0]);
        var button = form.querySelector('[type="submit"]');
        button.disabled = true;
        try {
            var result = await Api.postForm('/api/tournaments/' + context.tournamentId + '/registrations/' +
                form.dataset.registrationId + '/refund', body);
            Ui.showAlert(context.flash, 'success', result.message || 'Đã ghi nhận trạng thái hoàn lệ phí.');
            await context.reload();
        } catch (error) {
            Ui.showAlert(form.closest('[data-registration-id]').querySelector('.review-flash'),
                'danger',
                error.message || 'Không thể ghi nhận trạng thái hoàn.');
            button.disabled = false;
        }
    }

    window.RegistrationReviewPayments = { render: render };
})();
