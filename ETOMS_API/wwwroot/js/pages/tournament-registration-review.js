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
                    '/identity-documents/' + credentialId + '" data-private-file="giay-to-dinh-danh">Mở giấy tờ định danh</a>';
            }).join(' · ');
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(member.fullName) +
                ' · ' + Ui.escapeHtml(member.slot === 'STARTER' ? 'Chính thức' : 'Dự bị') +
                (member.positionName ? ' · <span class="badge badge-soft">' + Ui.escapeHtml(member.positionName) + '</span>' : '') + '</div>' +
                '<div class="item-sub">IGN: ' + Ui.escapeHtml(member.inGameName || 'Chưa khai báo') + '</div>' +
                (documents ? '<div class="item-sub">' + documents + '</div>' : '<div class="item-sub">Không có giấy tờ định danh được lưu.</div>') +
                '</div></div>';
        }).join('');
    }

    // Lịch sử kỷ luật chỉ tải khi Ban tổ chức MỞ mục đó (trước đây tải chi tiết từng đơn ngay khi vào trang và
    // lặp lại mỗi 30 giây — giải 32 đội là hơn 120 truy vấn database ở xa mỗi lượt). Đã tải thì nhớ lại cho các
    // lượt làm mới sau.
    var historyHtml = {};
    function historyMarkup(history) {
        return history.length
            ? history.map(function (penalty) {
                var severity = { WARNING: 'Cảnh cáo', MINOR: 'Lỗi nhẹ', MAJOR: 'Lỗi nặng', DISQUALIFICATION: 'Tước quyền thi đấu', CANCELLATION: 'Hủy kết quả' }[penalty.severity] || penalty.severity;
                var state = { ACTIVE: 'Còn hiệu lực', EXPIRED: 'Hết hiệu lực', CANCELLED: 'Đã hủy' }[penalty.status] || penalty.status;
                return '<div class="item-sub">' + Ui.escapeHtml(penalty.tournamentName) + ' · ' +
                    Ui.escapeHtml(severity) + ' · ' + Number(penalty.points) + ' điểm · ' +
                    Ui.escapeHtml(penalty.reason) + ' · ' + Ui.escapeHtml(state) + '</div>';
            }).join('')
            : '<div class="item-sub">Không có ghi nhận kỷ luật trước đây.</div>';
    }
    function wireDisciplineHistory(items) {
        items.forEach(function (item) {
            var card = root.querySelector('article[data-registration-id="' + item.registrationId + '"]');
            var box = card && card.querySelector('.discipline-history');
            if (!box) return;
            if (historyHtml[item.registrationId]) { box.innerHTML = historyHtml[item.registrationId]; return; }
            var details = box.closest('details');
            details.addEventListener('toggle', async function onOpen() {
                if (!details.open) return;
                details.removeEventListener('toggle', onOpen);
                try {
                    var response = await Api.get('/api/tournaments/' + tournamentId + '/registrations/' + item.registrationId);
                    historyHtml[item.registrationId] = historyMarkup(response.data.disciplineHistory || []);
                    box.innerHTML = historyHtml[item.registrationId];
                } catch (error) {
                    box.textContent = error.message || 'Không thể tải lịch sử kỷ luật.';
                    details.addEventListener('toggle', onOpen);   // cho thử lại lần mở sau
                }
            });
        });
    }

    function render(items, summary) {
        document.getElementById('tournament-name').textContent =
            'Xét duyệt: ' + (summary.tournamentName || (items[0] && items[0].tournamentName) || 'đăng ký giải');
        document.getElementById('registration-capacity').innerHTML =
            '<div class="registration-capacity-stat"><strong>' + Number(summary.availableSlots) + '</strong>' +
            '<span>suất còn trống / ' + Number(summary.maxTeams) + '</span>' +
            // Đơn đang chờ duyệt vẫn giữ suất (CountOccupiedSlotsAsync) — ghi rõ để BTC không tưởng là lỗi.
            '<small class="field-hint d-block">' + Number(summary.occupiedSlots) +
            ' suất đã có đội giữ (gồm cả đơn chờ duyệt)</small></div>' +
            '<div class="registration-capacity-stat"><strong>' + Number(summary.waitlistCount) + '</strong>' +
            '<span>đội đang chờ</span></div>';

        if (!items.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không có đơn phù hợp</h3>' +
                '<p>Thử đổi bộ lọc hoặc quay lại sau khi có đội gửi đăng ký.</p></div></div>';
            return;
        }
        // Giải 64–128 đội: phân trang 8 đơn/trang (góp ý 27/11; tìm theo tên đội đã có ở thanh lọc phía trên, gọi máy chủ).
        // Nút duyệt/lệ phí/hàng chờ gắn lại cho các đơn của trang vừa vẽ.
        var first = true;
        Ui.listPager(root, {
            items: items, pageSize: 8, listClass: 'feature-list', render: cardHtml,
            after: function (body) {
                var ids = Array.prototype.map.call(body.querySelectorAll('article[data-registration-id]'), function (a) { return Number(a.dataset.registrationId); });
                var pageItems = items.filter(function (it) { return ids.indexOf(it.registrationId) >= 0; });
                wireDisciplineHistory(pageItems);
                // Tiêu chí phân định (BRK-07) không thuộc việc duyệt đơn — cấu hình ở trang Chia nhánh > "Tiêu chí phân định".
                if (window.RegistrationReviewDecisions) window.RegistrationReviewDecisions.render(body, pageItems, context);
                if (window.RegistrationReviewPayments) window.RegistrationReviewPayments.render(body, pageItems, context);
                // Nút "Đôn đội đầu hàng chờ" chỉ chèn một lần (lần vẽ đầu), sang trang sau không chèn thêm.
                if (window.RegistrationReviewWaitlist)
                    window.RegistrationReviewWaitlist.render(body, pageItems, first ? summary : Object.assign({}, summary, { waitlistCount: 0 }), context);
                first = false;
            }
        });
    }

    function cardHtml(item) {
            return '<article class="feature-card" data-registration-id="' + item.registrationId + '">' +
                '<div class="d-flex justify-content-between gap-3 flex-wrap"><div><h3>' +
                Ui.escapeHtml(item.teamName) + ' [' + Ui.escapeHtml(item.teamTag) + ']</h3>' +
                '<div class="muted-copy">Nộp: ' +
                Ui.escapeHtml(item.submittedAt ? Ui.fmtDateTime(item.submittedAt) : '—') +
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
    }

    function hideManagementUi(message) {
        ['dev-banner', 'review-toolbar', 'review-list-section'].forEach(function (id) {
            var el = document.getElementById(id);
            if (el) el.style.display = 'none';
        });
        flash.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không có quyền truy cập</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p>' +
            '<a class="btn btn-ghost mt-3" href="/registration/organizer.html">Về Giải của tôi</a></div></div>';
    }

    async function load() {
        if (!Number.isInteger(tournamentId) || tournamentId <= 0) {
            hideManagementUi('Liên kết thiếu mã giải hợp lệ.');
            return;
        }
        try {
            var params = new URLSearchParams();
            params.set('status', document.getElementById('status-filter').value);
            params.set('search', document.getElementById('team-search').value.trim());
            var response = await Api.get('/api/tournaments/' + tournamentId + '/registrations?' + params.toString());
            render(response.data.registrations || [], response.data);
        } catch (error) {
            if (error.status === 404 || error.status === 403) {
                hideManagementUi(error.message || 'Bạn không có quyền quản lý giải này.');
            } else {
                root.innerHTML = '';
                Ui.showAlert(flash, 'danger', error.message || 'Không thể tải danh sách đăng ký.');
            }
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

        // Mục 10 (17/11): đội nộp đơn/giấy tờ mới thì Ban tổ chức thấy mà không cần tải lại trang — hỏi lại máy chủ
        // mỗi 30 giây khi tab đang mở. Bỏ qua lượt làm mới nếu đang gõ lý do / ghi chú để không mất chữ đang nhập.
        window.setInterval(function () {
            if (document.visibilityState !== 'visible') return;
            var typing = Array.from(root.querySelectorAll('input, textarea')).some(function (el) {
                return el === document.activeElement || (el.type !== 'file' && el.value);
            });
            if (!typing) load();
        }, 30000);
    });
})();
