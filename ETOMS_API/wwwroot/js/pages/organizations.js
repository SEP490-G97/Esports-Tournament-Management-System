(function () {
    var flash = document.getElementById('organization-flash');

    function showFailure(error, fallback) {
        Ui.showAlert(flash, 'danger', (error && error.message) || fallback);
    }

    function renderOrganizations(items) {
        var root = document.getElementById('organization-list');
        if (!items.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Chưa có đơn vị hoạt động</h3><p>Đơn vị đã được xét duyệt sẽ hiển thị tại đây.</p></div></div>';
            return;
        }
        root.innerHTML = '<div class="row g-3">' + items.map(function (item) {
            var logoUrl = Ui.safeUrl(item.logoUrl);
            var logo = logoUrl
                ? '<img src="' + Ui.escapeHtml(logoUrl) + '" alt="" />'
                : Ui.escapeHtml((item.name || '?').slice(0, 1));
            var rate = Math.round(Number(item.reputation.completionRate || 0) * 100);
            return '<div class="col-12 col-md-6 col-lg-4"><a class="org-card d-block" href="/organization/details.html?id=' + Number(item.organizationId) + '">' +
                '<div class="org-mark">' + logo + '</div><h3>' + Ui.escapeHtml(item.name) + '</h3>' +
                '<p>' + Ui.escapeHtml(item.description || 'Chưa có mô tả.') + '</p>' +
                '<div class="org-meta"><span class="badge badge-soft">' + Ui.escapeHtml(item.organizationTypeLabel) + '</span><span>' + Number(item.reputation.totalTournamentCount) + ' giải</span><span>' + rate + '% hoàn thành</span></div>' +
                '</a></div>';
        }).join('') + '</div>';
    }

    function renderReviewQueue(items, reviewerId) {
        var root = document.getElementById('review-queue');
        if (!items.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><p>Không có hồ sơ đang chờ xét duyệt.</p></div></div>';
            return;
        }
        root.innerHTML = '<div class="feature-list">' + items.map(function (item) {
            var docs = (item.documents || []).map(function (doc) {
                return '<div class="item-sub">' + Ui.escapeHtml(doc.documentType) + ' · ' + Ui.escapeHtml(doc.originalFileName) + '</div>';
            }).join('') || '<div class="item-sub">Chưa bổ sung giấy tờ.</div>';
            return '<article class="feature-card" data-review-id="' + Number(item.organizationId) + '">' +
                '<div class="d-flex justify-content-between gap-3 flex-wrap"><div><h3>' + Ui.escapeHtml(item.name) + '</h3><div class="muted-copy">' + Ui.escapeHtml(item.organizationType) + ' · gửi ' + Ui.escapeHtml(new Date(item.createdAt).toLocaleString('vi-VN')) + '</div></div><span class="badge badge-gold">' + Ui.escapeHtml(item.statusLabel) + '</span></div>' +
                '<div class="mt-3">' + docs + '</div><div class="field mt-3"><label for="review-reason-' + Number(item.organizationId) + '">Lý do từ chối (nếu từ chối)</label><input id="review-reason-' + Number(item.organizationId) + '" class="input review-reason" maxlength="1000" /></div>' +
                '<div class="btn-row"><button type="button" class="btn btn-primary" data-review-decision="APPROVE" data-reviewer="' + reviewerId + '">Duyệt</button><button type="button" class="btn btn-ghost" data-review-decision="REJECT" data-reviewer="' + reviewerId + '">Từ chối</button></div><div class="review-flash mt-3"></div>' +
                '</article>';
        }).join('') + '</div>';

        root.querySelectorAll('[data-review-decision]').forEach(function (button) {
            button.addEventListener('click', async function () {
                var card = button.closest('[data-review-id]');
                var decision = button.dataset.reviewDecision;
                var reason = card.querySelector('.review-reason').value.trim();
                var cardFlash = card.querySelector('.review-flash');
                if (decision === 'REJECT' && !reason) {
                    Ui.showAlert(cardFlash, 'danger', 'Nhập lý do từ chối trước khi tiếp tục.');
                    return;
                }
                button.disabled = true;
                try {
                    var result = await Api.post('/api/organizations/' + card.dataset.reviewId + '/review', {
                        Decision: decision,
                        Reason: reason || null,
                        ReviewerAccountId: Number(button.dataset.reviewer)
                    });
                    Ui.showAlert(cardFlash, 'success', result.message || 'Đã cập nhật kết quả xét duyệt.');
                    await loadReviewQueue(reviewerId);
                    await loadPublicOrganizations();
                } catch (error) {
                    Ui.showAlert(cardFlash, 'danger', error.message || 'Không thể cập nhật hồ sơ.');
                    button.disabled = false;
                }
            });
        });
    }

    async function loadPublicOrganizations() {
        var response = await Api.get('/api/organizations');
        renderOrganizations(response.data || []);
    }

    async function loadReviewQueue(reviewerId) {
        var response = await Api.get('/api/organizations/review-queue');
        renderReviewQueue(response.data || [], reviewerId);
    }

    document.addEventListener('DOMContentLoaded', async function () {
        try {
            await loadPublicOrganizations();
        } catch (error) {
            document.getElementById('organization-list').innerHTML = '';
            showFailure(error, 'Không thể tải danh sách đơn vị.');
        }

        try {
            var me = await Api.get('/api/dev-auth/me');
            if (me.isAuthenticated && Number(me.userId) > 0) {
                document.getElementById('review-section').hidden = false;
                await loadReviewQueue(Number(me.userId));
            }
        } catch (error) {
            showFailure(error, 'Không thể tải hàng đợi xét duyệt.');
        }
    });
})();
