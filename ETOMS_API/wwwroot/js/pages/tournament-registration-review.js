(function () {
    var tournamentId = Number(new URLSearchParams(location.search).get('tournamentId'));
    var root = document.getElementById('registration-list');
    var flash = document.getElementById('registration-flash');

    function statusLabel(status) {
        return {
            DRAFT: 'Bản nháp',
            PENDING: 'Chờ xét duyệt',
            APPROVED: 'Đã duyệt',
            REJECTED: 'Bị từ chối',
            WAITLISTED: 'Danh sách chờ',
            WITHDRAWN: 'Đã rút',
            DISQUALIFIED: 'Bị loại',
            EXPIRED: 'Hết hạn'
        }[status] || status;
    }

    function renderLineup(item) {
        return (item.lineup || []).map(function (member) {
            var documents = (member.identityDocumentCredentialIds || []).map(function (credentialId) {
                return '<a href="/api/registrations/' + item.registrationId + '/players/' + member.accountId +
                    '/identity-documents/' + credentialId + '">Mở giấy tờ định danh</a>';
            }).join(' · ');
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(member.fullName) +
                ' · ' + Ui.escapeHtml(member.slot === 'STARTER' ? 'Chính thức' : 'Dự bị') + '</div>' +
                '<div class="item-sub">IGN: ' + Ui.escapeHtml(member.inGameName || 'Chưa khai báo') + '</div>' +
                (documents ? '<div class="item-sub">' + documents + '</div>' : '<div class="item-sub">Không có giấy tờ định danh được lưu.</div>') +
                '</div></div>';
        }).join('');
    }

    async function loadDisciplineHistory(items) {
        await Promise.all(items.map(async function (item) {
            var card = root.querySelector('[data-registration-id="' + item.registrationId + '"]');
            if (!card) return;
            try {
                var response = await Api.get('/api/tournaments/' + tournamentId + '/registrations/' + item.registrationId);
                var history = response.data.disciplineHistory || [];
                card.querySelector('.discipline-history').innerHTML = history.length
                    ? history.map(function (penalty) {
                        return '<div class="item-sub">' + Ui.escapeHtml(penalty.tournamentName) + ' · ' +
                            Ui.escapeHtml(penalty.severity) + ' · ' + Number(penalty.points) + ' điểm · ' +
                            Ui.escapeHtml(penalty.reason) + ' · ' + Ui.escapeHtml(penalty.status) + '</div>';
                    }).join('')
                    : '<div class="item-sub">Không có ghi nhận kỷ luật trước đây.</div>';
            } catch (error) {
                card.querySelector('.discipline-history').textContent =
                    error.message || 'Không thể tải lịch sử kỷ luật.';
            }
        }));
    }

    function render(items, summary) {
        document.getElementById('tournament-name').textContent =
            'Xét duyệt: ' + (summary.tournamentName || (items[0] && items[0].tournamentName) || 'đăng ký giải');
        document.getElementById('registration-capacity').innerHTML =
            '<div class="registration-capacity-stat"><strong>' + Number(summary.availableSlots) + '</strong>' +
            '<span>suất còn trống / ' + Number(summary.maxTeams) + '</span></div>' +
            '<div class="registration-capacity-stat"><strong>' + Number(summary.waitlistCount) + '</strong>' +
            '<span>đội đang chờ</span></div>';

        root.innerHTML = items.length ? items.map(function (item) {
            return '<article class="feature-card" data-registration-id="' + item.registrationId + '">' +
                '<div class="d-flex justify-content-between gap-3 flex-wrap"><div><h3>' +
                Ui.escapeHtml(item.teamName) + ' [' + Ui.escapeHtml(item.teamTag) + ']</h3>' +
                '<div class="muted-copy">Nộp: ' +
                Ui.escapeHtml(item.submittedAt ? new Date(item.submittedAt).toLocaleString('vi-VN') : '—') +
                ' · Đồng ý Rulebook v' + Number(item.rulebookVersionAccepted || 0) + '</div></div>' +
                '<span class="badge badge-soft">' + Ui.escapeHtml(statusLabel(item.status)) + '</span></div>' +
                (item.reviewNote ? '<p class="field-hint">' + Ui.escapeHtml(item.reviewNote) + '</p>' : '') +
                '<details class="registration-review-details"><summary>Đội hình (' +
                (item.lineup || []).length + ')</summary><div class="feature-list mt-3">' +
                renderLineup(item) + '</div></details>' +
                '<details class="registration-review-details"><summary>Lịch sử kỷ luật của đội</summary>' +
                '<div class="discipline-history mt-3">Đang tải...</div></details>' +
                '<div class="registration-decision-actions"></div>' +
                '<div class="registration-payment-actions"></div>' +
                '<div class="registration-waitlist-actions"></div>' +
                '<div class="review-flash mt-3" aria-live="polite"></div></article>';
        }).join('') : '<div class="card-etoms"><div class="empty"><h3>Không có đơn phù hợp</h3>' +
            '<p>Thử đổi bộ lọc hoặc quay lại sau khi có đội gửi đăng ký.</p></div></div>';

        loadDisciplineHistory(items);
        if (window.RegistrationReviewTieBreakers) {
            window.RegistrationReviewTieBreakers.render(
                summary.tieBreakerOrder || [],
                context);
        }
        if (window.RegistrationReviewDecisions) {
            window.RegistrationReviewDecisions.render(root, items, context);
        }
        if (window.RegistrationReviewPayments) {
            window.RegistrationReviewPayments.render(root, items, context);
        }
        if (window.RegistrationReviewWaitlist) {
            window.RegistrationReviewWaitlist.render(root, items, summary, context);
        }
    }

    async function load() {
        if (!Number.isInteger(tournamentId) || tournamentId <= 0) {
            Ui.showAlert(flash, 'danger', 'Liên kết thiếu mã giải hợp lệ.');
            return;
        }
        try {
            var params = new URLSearchParams();
            params.set('status', document.getElementById('status-filter').value);
            params.set('search', document.getElementById('team-search').value.trim());
            var response = await Api.get('/api/tournaments/' + tournamentId + '/registrations?' + params.toString());
            render(response.data.registrations || [], response.data);
        } catch (error) {
            root.innerHTML = '';
            Ui.showAlert(flash, 'danger', error.message || 'Không thể tải danh sách đăng ký.');
        }
    }

    var context = {
        tournamentId: tournamentId,
        flash: flash,
        reload: load
    };
    window.RegistrationReview = context;

    var searchTimer;

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        document.getElementById('status-filter').addEventListener('change', load);
        document.getElementById('team-search').addEventListener('input', function () {
            window.clearTimeout(searchTimer);
            searchTimer = window.setTimeout(load, 250);
        });
        load();
    });
})();
