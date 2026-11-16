(function () {
    function render(root, items, context) {
        items.forEach(function (item) {
            if (item.status !== 'PENDING' || item.waitlistExpiresAt) return;
            var card = root.querySelector('[data-registration-id="' + item.registrationId + '"]');
            if (!card) return;
            card.querySelector('.registration-decision-actions').innerHTML =
                '<div class="field mt-3"><label for="review-reason-' + item.registrationId + '">Lý do từ chối</label>' +
                '<input id="review-reason-' + item.registrationId + '" class="input" maxlength="500" /></div>' +
                '<div class="btn-row mt-2"><button type="button" class="btn btn-primary" data-decision="APPROVE" data-registration-id="' +
                item.registrationId + '">Duyệt</button><button type="button" class="btn btn-ghost" data-decision="REJECT" data-registration-id="' +
                item.registrationId + '">Từ chối</button></div>';
        });

        root.querySelectorAll('[data-decision]').forEach(function (button) {
            button.addEventListener('click', function () {
                decide(button, context);
            });
        });
    }

    async function decide(button, context) {
        var card = button.closest('[data-registration-id]');
        var reason = card.querySelector('#review-reason-' + button.dataset.registrationId).value.trim();
        var decision = button.dataset.decision;
        if (decision === 'REJECT' && !reason) {
            Ui.showAlert(card.querySelector('.review-flash'), 'danger', 'Nhập lý do từ chối.');
            return;
        }

        card.querySelectorAll('[data-decision]').forEach(function (action) { action.disabled = true; });
        try {
            var result = await Api.post('/api/tournaments/' + context.tournamentId + '/registrations/' +
                button.dataset.registrationId + '/review', {
                decision: decision,
                reason: reason || null
            });
            Ui.showAlert(context.flash, 'success', result.message || 'Đã cập nhật đơn.');
            await context.reload();
        } catch (error) {
            Ui.showAlert(card.querySelector('.review-flash'), 'danger', error.message || 'Không thể cập nhật đơn.');
            card.querySelectorAll('[data-decision]').forEach(function (action) { action.disabled = false; });
        }
    }

    window.RegistrationReviewDecisions = { render: render };
})();
