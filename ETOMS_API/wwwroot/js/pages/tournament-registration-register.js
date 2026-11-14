(function () {
    var query = new URLSearchParams(location.search);
    var tournamentId = Number(query.get('tournamentId'));
    var promotionLinkCode = query.get('ref')
        || localStorage.getItem('etoms-affiliate-code:' + tournamentId);
    if (promotionLinkCode) {
        promotionLinkCode = promotionLinkCode.trim().toUpperCase();
        localStorage.setItem('etoms-affiliate-code:' + tournamentId, promotionLinkCode);
    }
    var options;
    var draftRegistrationId = Number(new URLSearchParams(location.search).get('draftRegistrationId')) || null;
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
        document.querySelectorAll('#registration-form [type="submit"]').forEach(function (button) {
            button.disabled = options.teams.length === 0;
        });
        var draft;
        if (draftRegistrationId) {
            var draftResponse = await Api.get('/api/registrations/' + draftRegistrationId);
            draft = draftResponse.data;
            if (draft.tournamentId !== tournamentId || draft.status !== 'DRAFT') {
                throw new Error('Bản nháp không thuộc giải này hoặc không còn chỉnh sửa được.');
            }
            document.getElementById('team-select').value = draft.teamId;
        }
        var paymentSection = document.getElementById('payment-section');
        paymentSection.hidden = options.entryFee <= 0;
        if (!paymentSection.hidden) {
            document.getElementById('payment-proof-hint').textContent = options.requirePaymentProof
                ? 'Giải yêu cầu biên lai trước khi gửi đơn. ETOMS chỉ lưu bằng chứng, không thực hiện hoặc xác minh giao dịch.'
                : 'Tải biên lai nếu có. ETOMS chỉ lưu bằng chứng, không thực hiện hoặc xác minh giao dịch.';
            var bankInfo = document.getElementById('payment-bank-info');
            bankInfo.textContent = options.bankAccountInfo || 'Ban tổ chức chưa cung cấp thông tin tài khoản nhận lệ phí.';
            document.getElementById('payment-amount').value = options.entryFee;
            document.getElementById('payment-proof').required = options.requirePaymentProof;
            document.getElementById('payment-proof-required').hidden = !options.requirePaymentProof;
        }
        renderRoster();
        if (draft) {
            (draft.lineup || []).forEach(function (member) {
                var slot = document.querySelector('.lineup-slot[data-account-id="' + member.accountId + '"]');
                if (slot) slot.value = member.slot;
            });
        }
        document.getElementById('team-select').addEventListener('change', renderRoster);
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

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
            var isDraft = event.submitter && event.submitter.name === 'saveAsDraft';
            if (!isDraft) {
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
            }

            var proofInput = document.getElementById('payment-proof');
            var file = proofInput.files[0];
            if (!isDraft && options.requirePaymentProof && !file) {
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
            body.append('saveAsDraft', isDraft ? 'true' : 'false');
            body.append('acceptRulebook', 'true');
            if (promotionLinkCode) body.append('promotionLinkCode', promotionLinkCode);
            var amount = document.getElementById('payment-amount').value;
            if (amount) body.append('declaredAmount', amount);
            if (file && !isDraft) body.append('paymentProof', file);

            var button = event.currentTarget.querySelector('[type="submit"]');
            button.disabled = true;
            try {
                var response = await Api.postForm('/api/tournaments/' + tournamentId + '/registrations', body);
                var resultMessage = response.message || (isDraft ? 'Đã lưu bản nháp.' : 'Đã gửi đơn đăng ký.');
                if (isDraft) draftRegistrationId = response.data.registrationId;
                Ui.showAlert(flash, 'success', resultMessage);
                document.getElementById('registration-panel').innerHTML =
                    '<div class="empty"><h2>' + (isDraft ? 'Đã lưu bản nháp' : 'Đã tiếp nhận đơn') +
                    '</h2><p>' + Ui.escapeHtml(resultMessage) +
                    '</p>' + (isDraft
                        ? '<a class="btn btn-primary" href="/registration/register.html?tournamentId=' + tournamentId +
                            '&draftRegistrationId=' + draftRegistrationId + '">Tiếp tục chỉnh sửa</a> '
                        : '') +
                    '<a class="btn btn-ghost" href="/registration/mine.html">Xem đơn đăng ký</a></div>';
            } catch (error) {
                showFailure(error, 'Không thể gửi đơn đăng ký.');
                button.disabled = false;
            }
        });
    });
})();
