(function () {
    var organizationId = Ui.qsInt('id');
    var currentUserId;
    var members = [];
    var canManageMembers = false;
    var flash = document.getElementById('organization-detail-flash');

    function showFailure(error, fallback, target) {
        Ui.showAlert(target || flash, 'danger', (error && error.message) || fallback);
    }

    function roleLabel(role) {
        return { CHAIR: 'Chủ nhiệm đơn vị', TOURNAMENT_OPERATOR: 'Điều hành giải', MARKETING: 'Truyền thông' }[role] || role;
    }

    function renderOrganization(org) {
        var reputation = org.reputation;
        var root = document.getElementById('organization-detail');
        var logoUrl = Ui.safeUrl(org.logoUrl);
        var websiteUrl = Ui.safeUrl(org.websiteUrl);
        var logo = logoUrl
            ? '<img class="org-logo-large" src="' + Ui.escapeHtml(logoUrl) + '" alt="" />'
            : '<div class="org-logo-large">' + Ui.escapeHtml((org.name || '?').slice(0, 1)) + '</div>';
        var link = websiteUrl ? '<a href="' + Ui.escapeHtml(websiteUrl) + '" target="_blank" rel="noopener noreferrer">' + Ui.escapeHtml(org.websiteUrl) + '</a>' : 'Chưa cung cấp';
        var tournaments = (org.tournaments || []).map(function (item) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(item.name) + '</div><div class="item-sub">' + Ui.escapeHtml(item.statusLabel) + ' · ' + Ui.escapeHtml(new Date(item.startAt).toLocaleDateString('vi-VN')) + '</div></div></div>';
        }).join('');
        root.innerHTML = '<section class="feature-card"><div class="team-hero">' + logo + '<div><span class="badge badge-soft">' + Ui.escapeHtml(org.organizationTypeLabel) + '</span><h2 class="mt-2 mb-1">' + Ui.escapeHtml(org.name) + '</h2><div class="team-hero sub">' + (org.parentOrganization ? '<span>Trực thuộc: ' + Ui.escapeHtml(org.parentOrganization) + '</span>' : '') + '<span>' + Ui.escapeHtml(org.contactEmail || 'Chưa có email liên hệ') + '</span></div></div></div>' +
            '<p class="mt-4 mb-2">' + Ui.escapeHtml(org.description || 'Đơn vị chưa có mô tả.') + '</p><div class="muted-copy">Website: ' + link + (org.contactPhone ? ' · Điện thoại: ' + Ui.escapeHtml(org.contactPhone) : '') + '</div>' +
            '<nav class="org-public-nav" aria-label="Các nội dung trong hồ sơ"><a href="#organization-reputation">Chỉ số hoạt động</a><a href="#organization-tournaments">Giải đấu</a></nav></section>' +
            '<div id="organization-reputation" class="row g-3 mt-2"><div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Giải đã hoàn thành</div><div class="num accent">' + Number(reputation.completedTournamentCount) + '<small> / ' + Number(reputation.totalTournamentCount) + '</small></div></div></div>' +
            '<div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Tỷ lệ hoàn thành</div><div class="num">' + Math.round(Number(reputation.completionRate || 0)) + '%</div></div></div>' +
            '<div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Trận tranh chấp</div><div class="num">' + Number(reputation.disputedMatchCount) + '<small> / ' + Number(reputation.totalMatchCount) + '</small></div></div></div>' +
            '<div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Tỷ lệ trận tranh chấp</div><div class="num">' + Math.round(Number(reputation.disputeRate || 0)) + '%</div></div></div>' +
            '<div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Thời gian xử lý tranh chấp TB</div><div class="num">' + (reputation.averageDisputeResolutionHours === null ? '—' : Number(reputation.averageDisputeResolutionHours).toFixed(1)) + '<small>' + (reputation.averageDisputeResolutionHours === null ? '' : ' giờ') + '</small></div></div></div>' +
            '<div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Hoàn tiền ghi nhận</div><div class="num">' + Number(reputation.recordedRefundCount) + '</div></div></div></div>' +
            '<section id="organization-tournaments" class="feature-card mt-4"><h2>Giải đấu của đơn vị</h2>' + (tournaments ? '<div class="feature-list mt-3">' + tournaments + '</div>' : '<p class="muted-copy mt-3">Chưa có giải đấu công khai.</p>') + '</section>';
    }

    function renderMembers() {
        var tbody = document.getElementById('organization-members');
        tbody.innerHTML = members.map(function (member) {
            var actions = '';
            if (member.status === 'INVITED' && member.accountId === currentUserId) {
                actions = '<button type="button" class="btn btn-primary btn-xs" data-accept="' + Number(member.organizationMemberId) + '">Chấp nhận</button>';
            } else if (canManageMembers && member.status === 'ACTIVE' && member.role !== 'CHAIR') {
                actions = '<select class="select member-role" aria-label="Vai trò thành viên" data-member-id="' + Number(member.organizationMemberId) + '">' +
                    '<option value="TOURNAMENT_OPERATOR"' + (member.role === 'TOURNAMENT_OPERATOR' ? ' selected' : '') + '>Điều hành giải</option>' +
                    '<option value="MARKETING"' + (member.role === 'MARKETING' ? ' selected' : '') + '>Truyền thông</option></select>';
            } else {
                actions = '<span class="muted-copy">—</span>';
            }
            return '<tr><td>' + Ui.escapeHtml(member.fullName) + ' <span class="item-sub">#' + Number(member.accountId) + '</span></td><td>' + Ui.escapeHtml(member.roleLabel || roleLabel(member.role)) + '</td><td>' + Ui.escapeHtml(member.statusLabel || member.status) + '</td><td>' + actions + '</td></tr>';
        }).join('') || '<tr><td colspan="4">Chưa có thành viên.</td></tr>';

        tbody.querySelectorAll('[data-accept]').forEach(function (button) {
            button.addEventListener('click', async function () {
                try {
                    var response = await Api.post('/api/organizations/' + organizationId + '/members/' + button.dataset.accept + '/accept');
                    Ui.showAlert(flash, 'success', response.message || 'Đã chấp nhận lời mời.');
                    await loadMembers();
                } catch (error) { showFailure(error, 'Không thể chấp nhận lời mời.'); }
            });
        });
        tbody.querySelectorAll('.member-role').forEach(function (select) {
            select.addEventListener('change', async function () {
                try {
                    var response = await Api.patch('/api/organizations/' + organizationId + '/members/' + select.dataset.memberId, { Role: select.value });
                    Ui.showAlert(flash, 'success', response.message || 'Đã cập nhật vai trò.');
                    await loadMembers();
                } catch (error) { showFailure(error, 'Không thể cập nhật vai trò.'); await loadMembers(); }
            });
        });
        var chairSelect = document.getElementById('new-chair');
        var candidates = members.filter(function (member) { return member.status === 'ACTIVE' && member.role !== 'CHAIR'; });
        chairSelect.innerHTML = candidates.map(function (member) {
            return '<option value="' + Number(member.accountId) + '">' + Ui.escapeHtml(member.fullName) + ' (#' + Number(member.accountId) + ')</option>';
        }).join('');
        document.getElementById('chair-transfer').hidden = candidates.length === 0;
    }

    async function loadMembers() {
        var response = await Api.get('/api/organizations/' + organizationId + '/members');
        members = response.data || [];
        renderMembers();
    }

    function wireManagement() {
        var inviteForm = document.getElementById('invite-form');
        inviteForm.addEventListener('submit', async function (event) {
            event.preventDefault();
            var accountId = Number(document.getElementById('invite-account').value);
            var role = document.getElementById('invite-role').value;
            if (!Number.isInteger(accountId) || accountId < 1) {
                Ui.showAlert(flash, 'danger', 'Nhập mã tài khoản là số nguyên dương.');
                return;
            }
            try {
                var response = await Api.post('/api/organizations/' + organizationId + '/members', { AccountId: accountId, Role: role });
                Ui.showAlert(flash, 'success', response.message || 'Đã gửi lời mời.');
                inviteForm.reset();
                await loadMembers();
            } catch (error) { showFailure(error, 'Không thể gửi lời mời.'); }
        });

        document.getElementById('transfer-form').addEventListener('submit', async function (event) {
            event.preventDefault();
            var newChairAccountId = Number(document.getElementById('new-chair').value);
            if (!newChairAccountId) return;
            var confirmed = window.etomsConfirm
                ? await window.etomsConfirm('Xác nhận chuyển quyền chủ nhiệm? Quyền quản lý sẽ được chuyển sang thành viên đã chọn.')
                : window.confirm('Xác nhận chuyển quyền chủ nhiệm? Quyền quản lý sẽ được chuyển sang thành viên đã chọn.');
            if (!confirmed) return;
            try {
                var response = await Api.post('/api/organizations/' + organizationId + '/chair-transfer', { NewChairAccountId: newChairAccountId });
                Ui.showAlert(flash, 'success', response.message || 'Đã chuyển chủ nhiệm.');
                await loadMembers();
            } catch (error) { showFailure(error, 'Không thể chuyển chủ nhiệm.'); }
        });

        var suspensionForm = document.getElementById('suspension-form');
        suspensionForm.addEventListener('submit', async function (event) {
            event.preventDefault();
            Ui.clearFieldErrors(suspensionForm);
            var reason = document.getElementById('suspension-reason').value.trim();
            if (!reason) {
                Ui.applyFieldErrors(suspensionForm, { Reason: 'Nhập lý do đình chỉ.' });
                return;
            }
            var confirmed = window.etomsConfirm
                ? await window.etomsConfirm('Đình chỉ đơn vị này? Thao tác sẽ thay đổi trạng thái hoạt động và hiện không có nút khôi phục trong bản thử nghiệm.')
                : window.confirm('Đình chỉ đơn vị này? Thao tác sẽ thay đổi trạng thái hoạt động và hiện không có nút khôi phục trong bản thử nghiệm.');
            if (!confirmed) return;
            try {
                var response = await Api.post('/api/organizations/' + organizationId + '/suspension', { Reason: reason });
                Ui.showAlert(flash, 'success', response.message || 'Đã đình chỉ đơn vị.');
                document.getElementById('organization-controls').hidden = true;
            } catch (error) {
                Ui.applyFieldErrors(suspensionForm, error.fieldErrors);
                showFailure(error, 'Không thể đình chỉ đơn vị.');
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        if (!organizationId || organizationId < 1) {
            showFailure(null, 'Liên kết hồ sơ đơn vị không hợp lệ.');
            document.getElementById('organization-detail').innerHTML = '';
            return;
        }
        var me = await Ui.currentUser();
        currentUserId = me ? Number(me.userId) : 0;
        try {
            var results = await Promise.all([
                Api.get('/api/organizations/' + organizationId),
                Api.get('/api/organizations/' + organizationId + '/members')
            ]);
            renderOrganization(results[0].data);
            members = results[1].data || [];
            var currentMembership = members.find(function (member) {
                return member.accountId === currentUserId && member.status === 'ACTIVE';
            });
            if (currentMembership && currentMembership.role === 'CHAIR') {
                canManageMembers = true;
                document.getElementById('organization-controls').hidden = false;
                wireManagement();
                renderMembers();
            } else if (members.some(function (member) {
                return member.accountId === currentUserId && member.status === 'INVITED';
            })) {
                document.getElementById('organization-controls').hidden = false;
                document.getElementById('invite-form').hidden = true;
                document.getElementById('chair-transfer').hidden = true;
                document.getElementById('suspension-panel').hidden = true;
                document.querySelector('.org-management-nav a[href="#chair-transfer"]').hidden = true;
                document.querySelector('.org-management-nav a[href="#suspension-panel"]').hidden = true;
                renderMembers();
            }
        } catch (error) {
            document.getElementById('organization-detail').innerHTML = '';
            showFailure(error, 'Không thể tải hồ sơ đơn vị.');
        }
    });
})();
