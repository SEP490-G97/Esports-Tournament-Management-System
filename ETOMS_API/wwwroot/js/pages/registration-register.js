(function () {
    var tournamentId = Number(new URLSearchParams(location.search).get('tournamentId'));
    var options;
    var flash = document.getElementById('registration-flash');

    function showFailure(error, fallback) {
        Ui.showAlert(flash, 'danger', (error && error.message) || fallback);
    }

    function formatDate(value) {
        return value ? new Date(value).toLocaleString('vi-VN') : '—';
    }

    function renderRoster() {
        var selectedTeamId = Number(document.getElementById('team-select').value);
        var team = options.teams.find(function (item) { return item.teamId === selectedTeamId; });
        var root = document.getElementById('roster-list');
        if (!team) {
            root.innerHTML = '<div class="item-sub">Không có đội chưa đăng ký phù hợp với tựa game này.</div>';
            return;
        }
        root.innerHTML = team.members.map(function (member) {
            var defaultSlot = member.teamSlot === 'STARTER' ? 'STARTER' : 'SUBSTITUTE';
            var missingIgn = options.requireInGameName && !member.inGameName;
            var missingDocument = options.requireIdentityDocument && !member.hasIdentityDocument;
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(member.fullName) + '</div>' +
                '<div class="item-sub">' + (member.inGameName ? 'IGN: ' + Ui.escapeHtml(member.inGameName) : 'Chưa khai báo IGN') +
                (missingDocument ? ' · Thiếu giấy tờ định danh' : '') + '</div></div>' +
                '<label class="visually-hidden" for="lineup-' + member.accountId + '">Vị trí của ' + Ui.escapeHtml(member.fullName) + '</label>' +
                '<select class="select lineup-slot" id="lineup-' + member.accountId + '" data-account-id="' + member.accountId + '">' +
                '<option value="">Không chọn</option>' +
                '<option value="STARTER"' + (defaultSlot === 'STARTER' ? ' selected' : '') + '>Chính thức</option>' +
                '<option value="SUBSTITUTE"' + (defaultSlot === 'SUBSTITUTE' ? ' selected' : '') + '>Dự bị</option>' +
                '</select>' +
                (missingIgn ? '<span class="badge badge-gold">Thiếu IGN</span>' : '') +
                (missingDocument ? '<span class="badge badge-gold">Thiếu giấy tờ</span>' : '') +
                '</div>';
        }).join('');
    }

    async function loadOptions() {
        if (!Number.isInteger(tournamentId) || tournamentId <= 0) {
            throw new Error('Liên kết đăng ký thiếu mã giải hợp lệ.');
        }
        var response = await Api.get('/api/tournaments/' + tournamentId + '/registration-options');
        options = response.data;
        document.getElementById('tournament-name').textContent = 'Đăng ký: ' + options.tournamentName;
        document.getElementById('tournament-summary').textContent =
            options.gameName + ' · Nhận đăng ký đến ' + formatDate(options.regCloseAt) +
            ' · Còn ' + options.availableSlots + '/' + options.maxTeams + ' suất.';
        document.getElementById('roster-rules').textContent =
            'Chọn đúng ' + options.starterCount + ' tuyển thủ chính thức và tối đa ' + options.maxSubstitutes +
            ' dự bị. IGN' + (options.requireInGameName ? ' và giấy tờ định danh' : '') +
            (options.requireIdentityDocument ? ' bắt buộc cho toàn bộ lineup.' : ' cần được khai báo nếu giải yêu cầu.');
        document.getElementById('rulebook-version').textContent = options.rulebookVersion;
        document.getElementById('rulebook-content').textContent = options.rulebookContent || 'Giải chưa công bố nội dung thể lệ.';
        document.getElementById('team-select').innerHTML = options.teams.length
            ? options.teams.map(function (team) {
                return '<option value="' + team.teamId + '">' + Ui.escapeHtml(team.teamName) + ' [' + Ui.escapeHtml(team.teamTag) + ']</option>';
            }).join('')
            : '<option value="">Không có đội đủ điều kiện</option>';
        document.querySelector('#registration-form [type="submit"]').disabled = options.teams.length === 0;
        var paymentSection = document.getElementById('payment-section');
        paymentSection.hidden = !(options.requirePaymentProof && options.entryFee > 0);
        if (!paymentSection.hidden) {
            document.getElementById('payment-amount').value = options.entryFee;
        }
        renderRoster();
        document.getElementById('team-select').addEventListener('change', renderRoster);
    }

    document.addEventListener('DOMContentLoaded', async function () {
        try {
            await loadOptions();
        } catch (error) {
            document.getElementById('registration-panel').hidden = true;
            showFailure(error, 'Không thể tải thông tin đăng ký.');
            return;
        }

        document.getElementById('registration-form').addEventListener('submit', async function (event) {
            event.preventDefault();
            Ui.clearFieldErrors(event.currentTarget);
            var lineup = Array.from(document.querySelectorAll('.lineup-slot'))
                .filter(function (select) { return !!select.value; })
                .map(function (select) {
                    return { accountId: Number(select.dataset.accountId), slot: select.value };
                });
            var starters = lineup.filter(function (member) { return member.slot === 'STARTER'; }).length;
            if (starters !== options.starterCount) {
                document.getElementById('lineup-error').textContent =
                    'Chọn chính xác ' + options.starterCount + ' tuyển thủ chính thức.';
                return;
            }
            if (!document.getElementById('accept-rulebook').checked) {
                Ui.showAlert(flash, 'danger', 'Bạn cần đồng ý với thể lệ giải trước khi gửi.');
                document.getElementById('accept-rulebook').focus();
                return;
            }

            var proofInput = document.getElementById('payment-proof');
            var file = proofInput.files[0];
            if (!document.getElementById('payment-section').hidden && !file) {
                Ui.showAlert(flash, 'danger', 'Giải yêu cầu tải biên lai lệ phí.');
                proofInput.focus();
                return;
            }
            if (file) {
                var extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
                if (!['.jpg', '.jpeg', '.png', '.pdf'].includes(extension) || file.size === 0 || file.size > 10 * 1024 * 1024) {
                    Ui.showAlert(flash, 'danger', 'Biên lai phải là JPG, PNG hoặc PDF và không vượt quá 10 MB.');
                    return;
                }
            }

            var body = new FormData();
            body.append('teamId', document.getElementById('team-select').value);
            body.append('lineupJson', JSON.stringify(lineup));
            body.append('acceptRulebook', 'true');
            var amount = document.getElementById('payment-amount').value;
            if (amount) body.append('declaredAmount', amount);
            if (file) body.append('paymentProof', file);

            var button = event.currentTarget.querySelector('[type="submit"]');
            button.disabled = true;
            try {
                var response = await Api.postForm('/api/tournaments/' + tournamentId + '/registrations', body);
                Ui.showAlert(flash, 'success', response.message || 'Đã gửi đơn đăng ký.');
                document.getElementById('registration-panel').innerHTML =
                    '<div class="empty"><h2>Đã tiếp nhận đơn</h2><p>' + Ui.escapeHtml(response.message || 'Theo dõi tiến độ tại mục đơn đăng ký của bạn.') +
                    '</p><a class="btn btn-primary" href="/registration/mine.html">Xem đơn đăng ký</a></div>';
            } catch (error) {
                showFailure(error, 'Không thể gửi đơn đăng ký.');
                button.disabled = false;
            }
        });
    });
})();
