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
        return { CHAIR: 'Chủ nhiệm đơn vị', TOURNAMENT_OPERATOR: 'Điều hành giải', MARKETING: 'Truyền thông', REFEREE: 'Trọng tài' }[role] || role;
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
            return '<a class="feature-list-item" href="/bracket/view.html?id=' + Number(item.tournamentId) + '"><div><div class="item-title">' + Ui.escapeHtml(item.name) + '</div><div class="item-sub">' + Ui.escapeHtml(item.statusLabel) + ' · ' + Ui.escapeHtml(Ui.parseUtc(item.startAt).toLocaleDateString('vi-VN')) + '</div></div><span class="muted-copy">Xem →</span></a>';
        }).join('');
        root.innerHTML = '<section class="feature-card"><div class="team-hero">' + logo + '<div><h2 class="mb-1">' + Ui.escapeHtml(org.name) + '</h2><div class="team-hero sub">' + (org.parentOrganization ? '<span>Trực thuộc: ' + Ui.escapeHtml(org.parentOrganization) + '</span>' : '') + '<span>' + Ui.escapeHtml(org.contactEmail || 'Chưa có email liên hệ') + '</span></div></div></div>' +
            '<p class="mt-4 mb-2">' + Ui.escapeHtml(org.description || 'Đơn vị chưa có mô tả.') + '</p><div class="muted-copy">Website: ' + link + (org.contactPhone ? ' · Điện thoại: ' + Ui.escapeHtml(org.contactPhone) : '') + '</div>' +
            '<nav class="org-public-nav" aria-label="Các nội dung trong hồ sơ"><a href="#organization-reputation">Chỉ số hoạt động</a><a href="#organization-tournaments">Giải đấu</a></nav></section>' +
            // 4 chỉ số chính trên một hàng (trước là 6 ô xếp 2 hàng lệch nhau).
            '<div id="organization-reputation" class="row g-3 mt-2"><div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Giải đã hoàn thành</div><div class="num accent">' + Number(reputation.completedTournamentCount) + '<small> / ' + Number(reputation.totalTournamentCount) + '</small></div></div></div>' +
            '<div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Tỷ lệ hoàn thành</div><div class="num">' + Math.round(Number(reputation.completionRate || 0)) + '%</div></div></div>' +
            '<div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Trận có tranh chấp</div><div class="num">' + Number(reputation.disputedMatchCount) + '<small> / ' + Number(reputation.totalMatchCount) + ' trận</small></div></div></div>' +
            '<div class="col-6 col-lg-3"><div class="stat-card h-100"><div class="label">Xử lý tranh chấp trung bình</div><div class="num">' + (reputation.averageDisputeResolutionHours === null ? '—' : Number(reputation.averageDisputeResolutionHours).toFixed(1)) + '<small>' + (reputation.averageDisputeResolutionHours === null ? '' : ' giờ') + '</small></div></div></div></div>' +
            '<section id="organization-tournaments" class="feature-card mt-4"><h2>Giải đấu của đơn vị</h2>' + (tournaments ? '<div class="feature-list mt-3">' + tournaments + '</div>' : '<p class="muted-copy mt-3">Chưa có giải đấu công khai.</p>') + '</section>';
        Ui.limitList(root.querySelector('#organization-tournaments .feature-list'), 10);   // đơn vị lớn có hàng trăm giải
    }

    function renderMembers() {
        var tbody = document.getElementById('organization-members');
        tbody.innerHTML = members.map(function (member) {
            var actions = '';
            if (member.status === 'INVITED' && member.accountId === currentUserId) {
                actions = '<button type="button" class="btn btn-primary btn-xs" data-accept="' + Number(member.organizationMemberId) + '">Chấp nhận</button>';
            } else if (canManageMembers && member.status === 'ACTIVE' && member.role !== 'CHAIR') {
                actions = '<select class="select member-role" aria-label="Đổi vai trò thành viên" data-member-id="' + Number(member.organizationMemberId) + '">' +
                    [['TOURNAMENT_OPERATOR', 'Điều hành giải'], ['REFEREE', 'Trọng tài'], ['MARKETING', 'Truyền thông']].map(function (r) {
                        return '<option value="' + r[0] + '"' + (member.role === r[0] ? ' selected' : '') + '>' + r[1] + '</option>';
                    }).join('') + '</select>';
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
            // Nhớ vai trò hiện tại để quay lại nếu người dùng hủy xác nhận.
            select.dataset.current = select.value;
            select.addEventListener('change', async function () {
                var member = members.find(function (m) { return String(m.organizationMemberId) === String(select.dataset.memberId); });
                var newLabel = select.options[select.selectedIndex].text;
                var ok = await window.etomsConfirm('Đổi vai trò của ' + (member ? member.fullName : 'thành viên') + ' thành "' + newLabel + '"?' +
                    (select.dataset.current === 'REFEREE' ? ' Nếu người này đang làm trọng tài ở trận chưa kết thúc thì không đổi được.' : ''),
                    { title: 'Đổi vai trò thành viên', okText: 'Đổi vai trò' });
                if (!ok) { select.value = select.dataset.current; return; }
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
            Ui.clearFieldErrors(inviteForm);
            var emailInput = document.getElementById('invite-email');
            var email = emailInput.value.trim();
            var role = document.getElementById('invite-role').value;
            if (!email || !emailInput.validity.valid) {
                Ui.applyFieldErrors(inviteForm, { Email: 'Nhập đúng email tài khoản Ban tổ chức cần mời.' });
                return;
            }
            try {
                var response = await Api.post('/api/organizations/' + organizationId + '/members', { Email: email, Role: role });
                Ui.showAlert(flash, 'success', response.message || 'Đã gửi lời mời.');
                inviteForm.reset();
                await loadMembers();
            } catch (error) {
                Ui.applyFieldErrors(inviteForm, error.fieldErrors);
                showFailure(error, 'Không thể gửi lời mời.');
            }
        });

        document.getElementById('transfer-form').addEventListener('submit', async function (event) {
            event.preventDefault();
            var newChairAccountId = Number(document.getElementById('new-chair').value);
            if (!newChairAccountId) return;
            var confirmed = await window.etomsConfirm('Xác nhận chuyển quyền chủ nhiệm? Quyền quản lý sẽ được chuyển sang thành viên đã chọn.', { title: 'Chuyển chủ nhiệm', okText: 'Chuyển quyền' });
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
            var confirmed = await window.etomsConfirm('Đình chỉ đơn vị này? Đơn vị sẽ ngừng hoạt động và các thành viên không tạo giải mới dưới tên đơn vị được nữa.', { title: 'Đình chỉ đơn vị', okText: 'Đình chỉ' });
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
            // Chủ nhiệm: thành viên + bàn giao. Đình chỉ đơn vị là việc của Admin (máy chủ chỉ cho Admin).
            var isAdmin = me && me.role === 'ADMIN';
            if ((currentMembership && currentMembership.role === 'CHAIR') || isAdmin) {
                canManageMembers = !!currentMembership;
                document.getElementById('organization-controls').hidden = false;
                wireManagement();
                renderMembers();
                var hide = function (sel) { var el = document.querySelector(sel); if (el) el.hidden = true; };
                if (!isAdmin) { hide('#suspension-panel'); hide('.org-management-nav a[href="#suspension-panel"]'); }
                if (!currentMembership) {
                    hide('#invite-form'); hide('#chair-transfer'); hide('.org-management-nav a[href="#chair-transfer"]');
                }
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
