(function () {
    var root = document.getElementById('my-registrations');
    var flash = document.getElementById('registration-flash');
    var registrations = [];

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
        root.innerHTML = items.map(function (item) {
            var lineup = (item.lineup || []).map(function (member) {
                return '<div class="item-sub">' + Ui.escapeHtml(member.slot === 'STARTER' ? 'Chính thức' : 'Dự bị') +
                    ' · ' + Ui.escapeHtml(member.fullName) + (member.inGameName ? ' · IGN: ' + Ui.escapeHtml(member.inGameName) : '') + '</div>';
            }).join('');
            var paymentRecords = (item.paymentRecords || []).map(function (record) {
                return '<div class="item-sub">' + Ui.escapeHtml(record.recordKind === 'REFUND' ? 'Hoàn tiền' : 'Biên lai') +
                    (record.declaredAmount !== null ? ' · ' + Number(record.declaredAmount).toLocaleString('vi-VN') + ' đ' : '') +
                    (record.downloadUrl ? ' · <a href="' + Ui.escapeHtml(record.downloadUrl) + '">Tải tệp</a>' : '') +
                    (record.note ? ' · ' + Ui.escapeHtml(record.note) : '') + '</div>';
            }).join('');
            var offer = item.waitlistExpiresAt
                ? '<div class="field-hint">Được mời nhận suất; xác nhận trước ' + Ui.escapeHtml(new Date(item.waitlistExpiresAt).toLocaleString('vi-VN')) + '.</div>' +
                    '<button type="button" class="btn btn-primary" data-accept-offer="' + item.registrationId + '">Xác nhận nhận suất</button>'
                : '';
            var proof = item.paymentStatus === 'UNPAID' && item.requirePaymentProof && item.entryFee > 0
                ? '<form class="payment-proof-form" data-registration-id="' + item.registrationId + '">' +
                    '<label>Biên lai lệ phí <input class="input" name="file" type="file" accept=".jpg,.jpeg,.png,.pdf" required /></label>' +
                    '<label>Số tiền khai báo <input class="input" name="declaredAmount" type="number" min="0" step="0.01" /></label>' +
                    '<button class="btn btn-ghost" type="submit">Tải biên lai</button></form>'
                : '';
            var withdraw = canWithdraw(item.status)
                ? '<button type="button" class="btn btn-ghost" data-withdraw="' + item.registrationId + '">Rút đơn</button>'
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
                    ' lúc ' + Ui.escapeHtml(new Date(item.consentAt).toLocaleString('vi-VN')) + '.</p>' : '') +
                '<div class="mt-3"><strong>Lineup</strong>' + lineup + '</div>' +
                '<div class="mt-3"><strong>Lệ phí: ' +
                (item.entryFee > 0 ? Number(item.entryFee).toLocaleString('vi-VN') + ' đ · ' + Ui.escapeHtml(paymentLabel(item.paymentStatus)) : 'Miễn phí') +
                '</strong>' + paymentRecords + '</div>' +
                offer + proof + '<div class="btn-row mt-3">' + editDraft + withdraw + '</div></article>';
        }).join('');

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
                if (!window.confirm('Rút đơn sẽ giải phóng suất và không thể khôi phục. Bạn có muốn tiếp tục?')) return;
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
                if (form.elements.declaredAmount.value) body.append('declaredAmount', form.elements.declaredAmount.value);
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

    document.addEventListener('DOMContentLoaded', function () {
        document.getElementById('registration-status-filter').addEventListener('change', applyFilters);
        document.getElementById('registration-search').addEventListener('input', applyFilters);
        load();
    });
})();
