(function () {
    function render(root, items, summary, context) {
        var capacity = document.getElementById('registration-capacity');
        capacity.insertAdjacentHTML('beforeend',
            '<button type="button" class="btn btn-primary mt-2" id="promote-first">Đôn đội đầu hàng chờ</button>');
        var promoteFirst = document.getElementById('promote-first');
        promoteFirst.disabled = summary.availableSlots < 1 || summary.waitlistCount < 1;
        promoteFirst.addEventListener('click', function () {
            promote(promoteFirst, context, null);
        });

        items.forEach(function (item) {
            var card = root.querySelector('[data-registration-id="' + item.registrationId + '"]');
            if (!card) return;
            var message = '';
            if (item.status === 'WAITLISTED') {
                message = '<p class="field-hint mt-3">Vị trí hàng chờ FIFO: ' +
                    Number(item.waitlistPosition) + '.</p>' +
                    '<button type="button" class="btn btn-ghost" data-promote-registration="' +
                    item.registrationId + '"' + (summary.availableSlots < 1 ? ' disabled' : '') +
                    '>Đôn đội này thủ công</button>';
            } else if (item.status === 'PENDING' && item.waitlistExpiresAt) {
                message = '<p class="field-hint mt-3">Được mời nhận suất; đội trưởng cần xác nhận trước ' +
                    Ui.escapeHtml(new Date(item.waitlistExpiresAt).toLocaleString('vi-VN')) + '.</p>';
            }
            card.querySelector('.registration-waitlist-actions').innerHTML = message;
        });
        root.querySelectorAll('[data-promote-registration]').forEach(function (button) {
            button.addEventListener('click', function () {
                promote(button, context, Number(button.dataset.promoteRegistration));
            });
        });
    }

    async function promote(button, context, registrationId) {
        var confirmation = registrationId === null
            ? 'Gửi lời mời suất trống cho đội đầu tiên theo thứ tự FIFO? Đội trưởng có 24 giờ để xác nhận.'
            : 'Gửi lời mời suất trống cho đội đã chọn, kể cả khi không đứng đầu hàng chờ? Đội trưởng có 24 giờ để xác nhận.';
        if (!window.confirm(confirmation)) return;
        button.disabled = true;
        try {
            var response = await Api.post('/api/tournaments/' + context.tournamentId + '/waitlist/promote', {
                registrationId: registrationId
            });
            Ui.showAlert(context.flash, 'success', response.message || 'Đã gửi lời mời nhận suất.');
            await context.reload();
        } catch (error) {
            Ui.showAlert(context.flash, 'danger', error.message || 'Không thể đôn đội.');
            button.disabled = false;
        }
    }

    window.RegistrationReviewWaitlist = { render: render };
})();
