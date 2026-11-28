(function () {
    var root = document.getElementById('my-registrations');
    var flash = document.getElementById('registration-flash');
    var registrations = [];
    // Đội trưởng lâu năm có thể có hàng trăm đơn: chỉ vẽ PAGE đơn đầu, bấm "Xem thêm" để mở tiếp.
    var PAGE = 10;
    var shown = PAGE;

    // Cảnh báo trước khi rút đơn (28/11, giống trang giải — bracket-view.js): lệ phí đã nộp không được hoàn; suất trống
    // mời ngay đội đầu danh sách chờ, đội đó được báo và phải nộp lệ phí mới vào giải.
    function withdrawWarning(r) {
        var fee = Number(r.entryFee) || 0;
        var paid = fee > 0 && (r.paymentStatus === 'PROOF_SUBMITTED' || r.paymentStatus === 'CONFIRMED');
        var money = fee.toLocaleString('vi-VN') + ' đ';
        var team = r.teamName || 'của bạn';
        if (r.status === 'DRAFT') return 'Hủy đơn nháp của đội ' + team + '? Đơn đã hủy không khôi phục được.';
        var head = r.status === 'WAITLISTED'
            ? 'Đội ' + team + ' sẽ rời danh sách chờ của giải.'
            : 'Đội ' + team + ' sẽ rời giải. Suất của đội được mời ngay cho đội đứng đầu danh sách chờ (nếu có) — đội đó nhận thông báo, có 24 giờ để xác nhận' +
              (fee > 0 ? ' và phải nộp lệ phí ' + money + ' mới được vào giải' : '') + '.';
        return head + (paid ? ' Lệ phí ' + money + ' đội đã nộp sẽ KHÔNG được hoàn lại.' : '') + ' Đơn đã rút không khôi phục được. Vẫn rút đơn?';
    }

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

    function paymentLabel(status) {
        return {
            UNPAID: 'Chưa nộp',
            PROOF_SUBMITTED: 'Chờ đối soát',
            CONFIRMED: 'Đã xác nhận',
            REFUNDED: 'Đã hoàn'
        }[status] || status;
    }

    function canWithdraw(status) {
        return ['DRAFT', 'PENDING', 'APPROVED', 'WAITLISTED'].includes(status);
    }

    function render(items) {
        if (!items.length) {
            var filtered = document.getElementById('registration-status-filter').value !== 'ALL'
                || document.getElementById('registration-search').value.trim();
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>' +
                (filtered ? 'Không tìm thấy đơn phù hợp' : 'Chưa có đơn đăng ký') +
                '</h3><p>' + (filtered
                    ? 'Thử thay đổi trạng thái hoặc từ khóa tìm kiếm.'
                    : 'Khi đội của bạn gửi đơn tham gia giải, trạng thái sẽ hiển thị tại đây.') +
                '</p>' + (filtered ? '<button type="button" class="btn btn-ghost" id="clear-registration-filters">Xóa bộ lọc</button>' : '') +
                '</div></div>';
            var clearButton = document.getElementById('clear-registration-filters');
            if (clearButton) clearButton.addEventListener('click', clearFilters);
            return;
        }
        var total = items.length;
        root.innerHTML = items.slice(0, shown).map(function (item) {
            var lineup = (item.lineup || []).map(function (member) {
                return '<div class="item-sub">' + Ui.escapeHtml(member.slot === 'STARTER' ? 'Chính thức' : 'Dự bị') +
                    ' · ' + Ui.escapeHtml(member.fullName) + (member.inGameName ? ' · IGN: ' + Ui.escapeHtml(member.inGameName) : '') +
                    (member.positionName ? ' · Vị trí: ' + Ui.escapeHtml(member.positionName) : '') + '</div>';
            }).join('');
            var paymentRecords = (item.paymentRecords || []).map(function (record) {
                return '<div class="item-sub">' + Ui.escapeHtml(record.recordKind === 'REFUND' ? 'Hoàn tiền' : 'Biên lai') +
                    (record.declaredAmount !== null ? ' · ' + Number(record.declaredAmount).toLocaleString('vi-VN') + ' đ' : '') +
                    (record.downloadUrl ? ' · <a href="' + Ui.escapeHtml(record.downloadUrl) + '" data-private-file="' + Ui.escapeHtml(record.fileName || 'bien-lai') + '">Xem tệp</a>' : '') +
                    (record.note ? ' · ' + Ui.escapeHtml(record.note) : '') + '</div>';
            }).join('');
            var offer = item.waitlistExpiresAt
                ? '<div class="field-hint">Được mời nhận suất; xác nhận trước ' + Ui.escapeHtml(Ui.fmtDateTime(item.waitlistExpiresAt)) + '.</div>' +
                    '<button type="button" class="btn btn-primary" data-accept-offer="' + item.registrationId + '">Xác nhận nhận suất</button>'
                : '';
            var proof = item.paymentStatus === 'UNPAID' && item.requirePaymentProof && item.entryFee > 0
                ? '<form class="payment-proof-form" data-registration-id="' + item.registrationId + '">' +
                    '<label>Biên lai lệ phí <input class="input" name="file" type="file" accept=".jpg,.jpeg,.png,.pdf" required /></label>' +
                    '<button class="btn btn-ghost" type="submit">Tải biên lai</button></form>'
                : '';
            var withdraw = canWithdraw(item.status)
                ? '<button type="button" class="btn btn-ghost" data-withdraw="' + item.registrationId + '">Rút đơn</button>'
                : '';
            // GRL: giải có bộ luật theo tựa game thì đội trưởng xếp vị trí cho từng người chính thức (trang tự báo nếu giải chưa chọn bộ luật / đã khóa).
            var positions = ['PENDING', 'APPROVED', 'WAITLISTED'].indexOf(item.status) >= 0
                ? '<a class="btn btn-ghost" href="/registration/lineup-positions.html?id=' + Number(item.registrationId) + '">Xếp vị trí đội hình</a>'
                : '';
            var editDraft = item.status === 'DRAFT'
                ? '<a class="btn btn-primary" href="/registration/register.html?tournamentId=' +
                    item.tournamentId + '&draftRegistrationId=' + item.registrationId + '">Tiếp tục chỉnh sửa nháp</a>'
                : '';
            return '<article class="feature-card registration-mine-card">' +
                '<div class="d-flex justify-content-between gap-3 flex-wrap"><div><h2>' + Ui.escapeHtml(item.tournamentName) + '</h2>' +
                '<div class="muted-copy">' + Ui.escapeHtml(item.teamName) + ' [' + Ui.escapeHtml(item.teamTag) + ']</div></div>' +
                '<span class="badge badge-soft">' + Ui.escapeHtml(statusLabel(item.status)) + '</span></div>' +
                (item.status === 'WAITLISTED' ? '<p class="field-hint mt-2">Vị trí chờ: ' + Number(item.waitlistPosition) + ' · ' + Number(item.waitlistAheadCount) + ' đội phía trước.</p>' : '') +
                (item.reviewNote ? '<p class="field-hint mt-2">' + Ui.escapeHtml(item.reviewNote) + '</p>' : '') +
                (item.rulebookVersionAccepted ? '<p class="item-sub mt-2">Đã đồng ý thể lệ phiên bản ' + Number(item.rulebookVersionAccepted) +
                    ' lúc ' + Ui.escapeHtml(Ui.fmtDateTime(item.consentAt)) + '.</p>' : '') +
                '<div class="mt-3"><strong>Đội hình</strong>' + lineup + '</div>' +
                '<div class="mt-3"><strong>Lệ phí: ' +
                (item.entryFee > 0 ? Number(item.entryFee).toLocaleString('vi-VN') + ' đ · ' + Ui.escapeHtml(paymentLabel(item.paymentStatus)) : 'Miễn phí') +
                '</strong>' + paymentRecords + '</div>' +
                offer + proof + '<div class="btn-row mt-3">' + editDraft + positions + withdraw + '</div></article>';
        }).join('') +
            (total > shown ? '<div class="btn-row justify-content-center mt-2"><button type="button" class="btn btn-ghost" id="registration-more">Xem thêm ' +
                Math.min(PAGE, total - shown) + ' đơn (còn ' + (total - shown) + ')</button></div>' : '');
        var more = document.getElementById('registration-more');
        if (more) more.addEventListener('click', function () { shown += PAGE; render(items); });

        root.querySelectorAll('[data-accept-offer]').forEach(function (button) {
            button.addEventListener('click', async function () {
                button.disabled = true;
                try {
                    var result = await Api.post('/api/registrations/' + button.dataset.acceptOffer + '/waitlist-confirmation', {});
                    Ui.showAlert(flash, 'success', result.message || 'Đã xác nhận suất.');
                    await load();
                } catch (error) {
                    Ui.showAlert(flash, 'danger', error.message || 'Không thể xác nhận suất.');
                    button.disabled = false;
                }
            });
        });
        root.querySelectorAll('[data-withdraw]').forEach(function (button) {
            button.addEventListener('click', async function () {
                var reg = registrations.find(function (r) { return Number(r.registrationId) === Number(button.dataset.withdraw); }) || {};
                if (!(await window.etomsConfirm(withdrawWarning(reg), { title: 'Rút đơn đăng ký', okText: 'Vẫn rút đơn' }))) return;
                button.disabled = true;
                try {
                    var result = await Api.post('/api/registrations/' + button.dataset.withdraw + '/withdraw', {});
                    Ui.showAlert(flash, 'success', result.message || 'Đã rút đơn.');
                    await load();
                } catch (error) {
                    Ui.showAlert(flash, 'danger', error.message || 'Không thể rút đơn.');
                    button.disabled = false;
                }
            });
        });
        root.querySelectorAll('.payment-proof-form').forEach(function (form) {
            form.addEventListener('submit', async function (event) {
                event.preventDefault();
                var file = form.elements.file.files[0];
                if (!file) return;
                var body = new FormData();
                body.append('file', file);
                var button = form.querySelector('[type="submit"]');
                button.disabled = true;
                try {
                    var result = await Api.postForm('/api/registrations/' + form.dataset.registrationId + '/payment-proof', body);
                    Ui.showAlert(flash, 'success', result.message || 'Đã tải biên lai.');
                    await load();
                } catch (error) {
                    Ui.showAlert(flash, 'danger', error.message || 'Không thể tải biên lai.');
                    button.disabled = false;
                }
            });
        });
    }

    async function load() {
        try {
            var response = await Api.get('/api/registrations/mine');
            registrations = response.data || [];
            applyFilters();
        } catch (error) {
            root.innerHTML = '';
            Ui.showAlert(flash, 'danger', error.message || 'Không thể tải đơn đăng ký.');
        }
    }

    function applyFilters() {
        shown = PAGE;
        var status = document.getElementById('registration-status-filter').value;
        var term = document.getElementById('registration-search').value.trim().toLocaleLowerCase('vi');
        var filtered = registrations.filter(function (item) {
            var matchesStatus = status === 'ALL' || item.status === status;
            var searchable = (item.tournamentName || '') + ' ' + (item.teamName || '') + ' ' + (item.teamTag || '');
            return matchesStatus && (!term || searchable.toLocaleLowerCase('vi').includes(term));
        });
        var actionable = registrations.filter(function (item) {
            return ['DRAFT', 'PENDING', 'WAITLISTED'].includes(item.status);
        }).length;
        document.getElementById('registration-summary').innerHTML =
            '<strong>' + registrations.length + '</strong> đơn tất cả' +
            '<span class="registration-summary-separator" aria-hidden="true">·</span>' +
            '<strong>' + actionable + '</strong> cần theo dõi';
        render(filtered);
    }

    function clearFilters() {
        document.getElementById('registration-status-filter').value = 'ALL';
        document.getElementById('registration-search').value = '';
        applyFilters();
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['PLAYER']);
        if (!me) return;

        document.getElementById('registration-status-filter').addEventListener('change', applyFilters);
        document.getElementById('registration-search').addEventListener('input', applyFilters);
        load();
    });
})();
