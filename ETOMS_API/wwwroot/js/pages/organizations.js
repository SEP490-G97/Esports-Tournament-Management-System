(function () {
    var flash = document.getElementById('organization-flash');
    var organizations = [];
    var orgPage = 1, ORG_PAGE_SIZE = 12;   // 12 thẻ/trang (lưới 3 cột)

    function showFailure(error, fallback) {
        Ui.showAlert(flash, 'danger', (error && error.message) || fallback);
    }


    function renderOrganizations(items, hasSearch) {
        var root = document.getElementById('organization-list');
        if (!items.length) {
            var title = hasSearch ? 'Không tìm thấy đơn vị phù hợp' : 'Chưa có đơn vị hoạt động';
            var message = hasSearch ? 'Thử thay đổi từ khóa tìm kiếm.' : 'Đơn vị đã được xét duyệt sẽ hiển thị tại đây.';
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>' + title + '</h3><p>' + message + '</p></div></div>';
            return;
        }
        var pages = Math.max(1, Math.ceil(items.length / ORG_PAGE_SIZE));
        if (orgPage > pages) orgPage = pages;
        root.innerHTML = '<div class="row g-3">' + items.slice((orgPage - 1) * ORG_PAGE_SIZE, orgPage * ORG_PAGE_SIZE).map(function (item) {
            var logoUrl = Ui.safeUrl(item.logoUrl);
            var logo = logoUrl
                ? '<img src="' + Ui.escapeHtml(logoUrl) + '" alt="" />'
                : Ui.escapeHtml((item.name || '?').slice(0, 1));
            var rate = Math.round(Number(item.reputation.completionRate || 0));
            return '<div class="col-12 col-md-6 col-lg-4"><a class="org-card d-block" href="/organization/details.html?id=' + Number(item.organizationId) + '">' +
                '<div class="org-mark">' + logo + '</div><h3>' + Ui.escapeHtml(item.name) + '</h3>' +
                '<p>' + Ui.escapeHtml(item.description || 'Chưa có mô tả.') + '</p>' +
                '<div class="org-meta"><span>' + Number(item.reputation.totalTournamentCount) + ' giải</span><span>' + rate + '% hoàn thành</span></div>' +
                '</a></div>';
        }).join('') + '</div>' + Ui.pagerHtml(orgPage, pages);
        root.querySelectorAll('[data-page]').forEach(function (b) {
            b.addEventListener('click', function () {
                orgPage = parseInt(b.dataset.page, 10);
                renderOrganizations(items, hasSearch);
                root.scrollIntoView({ behavior: 'smooth', block: 'start' });
            });
        });
    }

    function applyOrganizationFilters() {
        orgPage = 1;
        var term = document.getElementById('organization-search').value.trim().toLocaleLowerCase('vi');
        var filtered = organizations.filter(function (item) {
            var matchesName = !term || (item.name || '').toLocaleLowerCase('vi').includes(term);
            return matchesName;
        });
        renderOrganizations(filtered, !!term);
    }

    function renderReviewQueue(items, reviewerId) {
        var root = document.getElementById('review-queue');
        if (!items.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><p>Không có hồ sơ đang chờ xét duyệt.</p></div></div>';
            return;
        }
        root.innerHTML = '<div class="feature-list">' + items.map(function (item) {
            var docs = (item.documents || []).map(function (doc) {
                var extension = (doc.originalFileName || '').split('.').pop().slice(0, 4).toUpperCase();
                return '<div class="review-document"><span class="review-document-mark">' + Ui.escapeHtml(extension || 'TỆP') + '</span><div><div class="item-title">' + Ui.escapeHtml(doc.documentType) + '</div><div class="item-sub">' + Ui.escapeHtml(doc.originalFileName) + '</div></div></div>';
            }).join('') || '<div class="item-sub">Chưa bổ sung giấy tờ.</div>';
            var uploadedTypes = (item.documents || []).map(function (doc) { return doc.documentType; });
            var checklist = (item.requiredDocumentTypes || []).map(function (type) {
                var uploaded = uploadedTypes.includes(type);
                return '<div class="feature-list-item"><div class="item-title">' + Ui.escapeHtml(type) +
                    '</div><span class="badge ' + (uploaded ? 'badge-active' : 'badge-gold') + '">' +
                    (uploaded ? 'Đã tải' : 'Còn thiếu') + '</span></div>';
            }).join('');
            var missingRequiredDocument = (item.requiredDocumentTypes || []).some(function (type) {
                return !uploadedTypes.includes(type);
            });
            return '<article class="feature-card" data-review-id="' + Number(item.organizationId) + '">' +
                '<div class="d-flex justify-content-between gap-3 flex-wrap"><div><h3>' + Ui.escapeHtml(item.name) + '</h3><div class="muted-copy">' + 'Gửi ' + Ui.escapeHtml(Ui.fmtDateTime(item.createdAt)) + '</div></div><span class="badge badge-gold">' + Ui.escapeHtml(item.statusLabel) + '</span></div>' +
                '<div class="feature-list mt-3">' + checklist + '</div><div class="review-documents mt-3">' + docs + '</div>' +
                '<div class="field mt-3"><label for="review-reason-' + Number(item.organizationId) + '">Lý do từ chối</label><input id="review-reason-' + Number(item.organizationId) + '" class="input review-reason" maxlength="1000" placeholder="Bắt buộc nếu từ chối hồ sơ" /></div>' +
                '<div class="btn-row"><button type="button" class="btn btn-primary" data-review-decision="APPROVE" data-reviewer="' + reviewerId + '"' + (missingRequiredDocument ? ' disabled title="Cần đủ giấy tờ bắt buộc mới có thể duyệt."' : '') + '>Duyệt</button><button type="button" class="btn btn-ghost" data-review-decision="REJECT" data-reviewer="' + reviewerId + '">Từ chối</button></div><div class="review-flash mt-3"></div>' +
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
                        Reason: reason || null
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
        organizations = response.data || [];
        applyOrganizationFilters();
    }

    // "Đơn vị của tôi" (Ban tổ chức): đơn vị đang tham gia, đơn vị chờ duyệt, lời mời chờ chấp nhận.
    async function loadMine() {
        var section = document.getElementById('my-orgs');
        var root = document.getElementById('my-orgs-list');
        section.hidden = false;
        var response = await Api.get('/api/organizations/mine');
        var items = response.data || [];
        root.innerHTML = items.length ? items.map(function (o) {
            var invited = o.memberStatus === 'INVITED';
            var action = invited
                ? '<span class="d-flex gap-2 flex-wrap"><button type="button" class="btn btn-primary btn-sm" data-accept-org="' + Number(o.organizationId) + '" data-member="' + Number(o.organizationMemberId) + '">Chấp nhận lời mời</button>' +
                  '<button type="button" class="btn btn-ghost btn-sm" data-decline-org="' + Number(o.organizationId) + '" data-member="' + Number(o.organizationMemberId) + '">Từ chối</button></span>'
                : '<a class="btn btn-ghost btn-sm" href="/organization/details.html?id=' + Number(o.organizationId) + '">' + (o.role === 'CHAIR' ? 'Quản lý đơn vị' : 'Xem đơn vị') + '</a>';
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(o.name) + '</div>' +
                '<div class="item-sub">' + Ui.escapeHtml(o.roleLabel) + ' · ' + Ui.escapeHtml(invited ? 'Được mời, chưa chấp nhận' : o.memberStatusLabel) +
                ' · Đơn vị: ' + Ui.escapeHtml(o.statusLabel) + '</div></div>' + action + '</div>';
        }).join('') : '<div class="card-etoms"><div class="empty"><p>Bạn chưa thuộc đơn vị nào. Bấm "Đăng ký đơn vị" để lập mới, hoặc nhờ chủ nhiệm một đơn vị mời bạn bằng email tài khoản này.</p></div></div>';
        root.querySelectorAll('[data-accept-org]').forEach(function (button) {
            button.addEventListener('click', async function () {
                button.disabled = true;
                try {
                    var res = await Api.post('/api/organizations/' + button.dataset.acceptOrg + '/members/' + button.dataset.member + '/accept');
                    Ui.showAlert(flash, 'success', res.message || 'Đã tham gia đơn vị.');
                    await loadMine();
                } catch (error) {
                    showFailure(error, 'Không thể chấp nhận lời mời.');
                    button.disabled = false;
                }
            });
        });
        // Từ chối lời mời (góp ý 28/11 — trước đây chỉ có nút Chấp nhận). Chủ nhiệm đơn vị được báo.
        root.querySelectorAll('[data-decline-org]').forEach(function (button) {
            button.addEventListener('click', async function () {
                if (!(await window.etomsConfirm('Từ chối lời mời vào đơn vị này? Chủ nhiệm đơn vị sẽ được báo.', { title: 'Từ chối lời mời', okText: 'Từ chối' }))) return;
                button.disabled = true;
                try {
                    var res = await Api.post('/api/organizations/' + button.dataset.declineOrg + '/members/' + button.dataset.member + '/decline');
                    Ui.showAlert(flash, 'success', res.message || 'Đã từ chối lời mời.');
                    await loadMine();
                } catch (error) {
                    showFailure(error, 'Không thể từ chối lời mời.');
                    button.disabled = false;
                }
            });
        });
        if (Ui.qs('mine')) section.scrollIntoView({ block: 'start' });
    }

    async function loadReviewQueue(reviewerId) {
        var response = await Api.get('/api/organizations/review-queue');
        renderReviewQueue(response.data || [], reviewerId);
    }

    document.addEventListener('DOMContentLoaded', async function () {
        document.getElementById('organization-search').addEventListener('input', applyOrganizationFilters);
        try {
            await loadPublicOrganizations();
        } catch (error) {
            document.getElementById('organization-list').innerHTML = '';
            showFailure(error, 'Không thể tải danh sách đơn vị.');
        }

        try {
            var me = await Ui.currentUser();
            var role = me && me.isAuthenticated ? me.role : null;
            document.getElementById('org-register-btn').hidden = role !== 'ORGANIZER';
            document.getElementById('org-player-note').hidden = role !== 'PLAYER';
            if (role === 'ORGANIZER') await loadMine();
            if (me && me.role === 'ADMIN') {
                document.getElementById('review-section').hidden = false;
                await loadReviewQueue(Number(me.userId));
            }
        } catch (error) {
            showFailure(error, 'Không thể tải hàng đợi xét duyệt.');
        }
    });
})();
