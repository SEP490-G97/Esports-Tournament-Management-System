(function () {
    var labels = {
        HEAD_TO_HEAD: 'Đối đầu trực tiếp',
        GAME_DIFFERENCE: 'Hiệu số ván',
        TOTAL_GAMES_WON: 'Tổng ván thắng',
        RANDOM_DRAW: 'Bốc thăm'
    };
    var order = [];

    function render(nextOrder, context) {
        order = nextOrder.slice();
        var list = document.getElementById('tie-breaker-list');
        list.innerHTML = order.map(function (criterion, index) {
            return '<li class="feature-list-item"><span>' + (index + 1) + '. ' +
                Ui.escapeHtml(labels[criterion] || criterion) + '</span><span>' +
                '<button type="button" class="btn btn-ghost" data-move-up="' + index + '"' +
                (index === 0 ? ' disabled' : '') + ' aria-label="Đưa lên">↑</button>' +
                '<button type="button" class="btn btn-ghost" data-move-down="' + index + '"' +
                (index === order.length - 1 ? ' disabled' : '') + ' aria-label="Đưa xuống">↓</button>' +
                '</span></li>';
        }).join('');

        list.querySelectorAll('[data-move-up]').forEach(function (button) {
            button.addEventListener('click', function () {
                move(Number(button.dataset.moveUp), -1, context);
            });
        });
        list.querySelectorAll('[data-move-down]').forEach(function (button) {
            button.addEventListener('click', function () {
                move(Number(button.dataset.moveDown), 1, context);
            });
        });
    }

    function move(index, offset, context) {
        var nextIndex = index + offset;
        if (nextIndex < 0 || nextIndex >= order.length) return;
        [order[index], order[nextIndex]] = [order[nextIndex], order[index]];
        render(order, context);
    }

    async function save(context) {
        var button = document.getElementById('save-tie-breakers');
        button.disabled = true;
        try {
            var response = await Api.put('/api/tournaments/' + context.tournamentId + '/tie-breakers', {
                order: order
            });
            Ui.showAlert(context.flash, 'success', response.message || 'Đã lưu thứ tự tie-breaker.');
        } catch (error) {
            Ui.showAlert(context.flash, 'danger', error.message || 'Không thể lưu thứ tự tie-breaker.');
        } finally {
            button.disabled = false;
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.getElementById('save-tie-breakers').addEventListener('click', function () {
            save(window.RegistrationReview);
        });
    });

    window.RegistrationReviewTieBreakers = { render: render };
})();
