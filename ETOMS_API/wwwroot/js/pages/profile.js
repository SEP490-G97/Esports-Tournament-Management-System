(function () {
    var accountId;
    var flash;
    var linkedGameAccounts = [];

    function showError(container, error, fallback) {
        Ui.showAlert(container, 'danger', (error && error.message) || fallback);
    }

    function renderList(root, items, emptyText, renderer) {
        if (!items || !items.length) {
            root.innerHTML = '<div class="empty"><p>' + Ui.escapeHtml(emptyText) + '</p></div>';
            return;
        }
        root.innerHTML = '<div class="feature-list">' + items.map(renderer).join('') + '</div>';
    }

    async function loadProfile() {
        var response = await Api.get('/api/profiles/' + accountId);
        var profile = response.data;
        document.getElementById('profile-email').textContent = profile.email || '';
        document.getElementById('profile-nav-name').textContent = profile.fullName || 'Người chơi';
        var avatarUrl = Ui.safeUrl(profile.avatarUrl);
        document.getElementById('profile-nav-avatar').innerHTML = avatarUrl
            ? '<img src="' + Ui.escapeHtml(avatarUrl) + '" alt="" />'
            : Ui.escapeHtml((profile.fullName || 'P').trim().slice(0, 1).toUpperCase());
        document.getElementById('profile-full-name').value = profile.fullName || '';
        document.getElementById('profile-avatar').value = profile.avatarUrl || '';
        document.getElementById('profile-dob').value = profile.dateOfBirth || '';
        document.getElementById('profile-phone').value = profile.phone || '';
        document.getElementById('profile-social').value = profile.socialLinks || '';
    }

    async function loadGames() {
        var [profileResponse, summary] = await Promise.all([
            Api.get('/api/profiles/' + accountId + '/public'),
            Api.get('/api/home/summary')
        ]);
        var profile = profileResponse.data;
        var select = document.getElementById('game-id');
        var gameOptions = summary.games || [];
        linkedGameAccounts = profile.gameAccounts || [];
        select.innerHTML = '<option value="">Chọn tựa game</option>' + gameOptions.map(function (game) {
            return '<option value="' + Number(game.gameId) + '">' + Ui.escapeHtml(game.title) + '</option>';
        }).join('');
        updateGameAccountForm();
        renderList(document.getElementById('profile-games'), linkedGameAccounts, 'Chưa liên kết tài khoản game.', function (game) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(game.gameTitle) + '</div><div class="item-sub">' + Ui.escapeHtml(game.inGameName) + '</div></div></div>';
        });
        return profile;
    }

    function updateGameAccountForm() {
        var select = document.getElementById('game-id');
        var input = document.getElementById('in-game-name');
        var existing = linkedGameAccounts.find(function (item) {
            return Number(item.gameId) === Number(select.value);
        });
        input.value = existing ? existing.inGameName : '';
        document.getElementById('game-submit').textContent = existing ? 'Cập nhật tên game' : 'Thêm tên game';
        document.getElementById('game-account-hint').textContent = existing
            ? 'Đã có tên liên kết cho tựa game này. Lưu sẽ cập nhật tên hiện tại, không tạo liên kết trùng.'
            : 'Mỗi tựa game chỉ có một tên liên kết trong phiên bản hiện tại.';
    }

    async function loadIdentityDocuments() {
        var response = await Api.get('/api/profiles/' + accountId + '/identity-documents');
        var documents = response.data;
        renderList(document.getElementById('identity-documents'), documents, 'Chưa có giấy tờ xác minh.', function (doc) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(doc.credentialTypeLabel || doc.credentialType) + '</div><div class="item-sub">Đã gửi ' + Ui.escapeHtml(new Date(doc.createdAt).toLocaleString('vi-VN')) + '</div></div><a class="btn btn-ghost btn-sm" href="/api/profiles/' + accountId + '/identity-documents/' + Number(doc.credentialId) + '/file">Tải bản đã gửi</a></div>';
        });
    }

    async function loadPreferences() {
        var response = await Api.get('/api/profiles/' + accountId + '/notification-preferences');
        var preferences = response.data;
        document.querySelector('[name="NotifyInApp"]').checked = !!preferences.notifyInApp;
        document.querySelector('[name="NotifyEmail"]').checked = !!preferences.notifyEmail;
    }

    async function loadPublicProfile() {
        var response = await Api.get('/api/profiles/' + accountId + '/public');
        var profile = response.data;
        var root = document.getElementById('public-profile');
        var gameAccounts = (profile.gameAccounts || []).map(function (item) {
            return '<span class="badge badge-soft">' + Ui.escapeHtml(item.gameTitle) + ': ' + Ui.escapeHtml(item.inGameName) + '</span>';
        }).join(' ');
        var teams = (profile.teams || []).map(function (item) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(item.teamName) + '</div><div class="item-sub">' + Ui.escapeHtml(item.gameTitle) + '</div></div></div>';
        }).join('');
        var titles = (profile.titles || []).map(function (item) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(item.tournamentName) + '</div><div class="item-sub">' + Ui.escapeHtml(item.placementLabel || item.placement) + '</div></div></div>';
        }).join('');
        var avatarUrl = Ui.safeUrl(profile.avatarUrl);
        root.innerHTML = '<div class="d-flex align-items-center gap-3 mb-3"><div class="org-mark">' +
            (avatarUrl ? '<img src="' + Ui.escapeHtml(avatarUrl) + '" alt="" />' : Ui.escapeHtml((profile.fullName || '?').slice(0, 1))) +
            '</div><div><h3 class="mb-1">' + Ui.escapeHtml(profile.fullName) + '</h3><div class="muted-copy">Mã người chơi #' + Number(profile.accountId) + '</div></div></div>' +
            '<p><a class="btn btn-ghost btn-sm" href="/profile/public.html?id=' + Number(profile.accountId) + '">Mở trang hồ sơ công khai</a></p>' +
            '<div class="d-flex flex-wrap gap-2 mb-3">' + (gameAccounts || '<span class="muted-copy">Chưa có tài khoản game công khai.</span>') + '</div>' +
            '<div class="row g-3"><div class="col-md-6"><h3 class="section-title">Đội tuyển</h3>' + (teams ? '<div class="feature-list">' + teams + '</div>' : '<p class="muted-copy">Chưa tham gia đội tuyển.</p>') + '</div>' +
            '<div class="col-md-6"><h3 class="section-title">Thành tích</h3>' + (titles ? '<div class="feature-list">' + titles + '</div>' : '<p class="muted-copy">Chưa có thành tích.</p>') + '</div></div>';
    }

    async function loadMatchHistory() {
        var response = await Api.get('/api/profiles/' + accountId + '/match-history');
        var matches = response.data;
        renderList(document.getElementById('match-history'), matches, 'Chưa có lịch sử trận đấu.', function (match) {
            var score = match.scoreFor === null || match.scoreAgainst === null ? 'Chưa có kết quả' : match.scoreFor + ' - ' + match.scoreAgainst;
            var schedule = match.scheduledAt ? new Date(match.scheduledAt).toLocaleString('vi-VN') : 'Chưa xếp lịch';
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(match.tournamentName) + ' · ' + Ui.escapeHtml(match.teamName) + '</div><div class="item-sub">' + Ui.escapeHtml(match.slot) + ' · ' + Ui.escapeHtml(schedule) + '</div></div><span class="badge badge-soft">' + Ui.escapeHtml(match.statusLabel || match.status) + ' · ' + Ui.escapeHtml(score) + '</span></div>';
        });
    }

    function wireForms() {
        var form = document.getElementById('profile-form');
        form.addEventListener('submit', async function (event) {
            event.preventDefault();
            Ui.clearFieldErrors(form);
            try {
                var data = new FormData(form);
                var body = {
                    FullName: data.get('FullName'),
                    AvatarUrl: data.get('AvatarUrl') || null,
                    DateOfBirth: data.get('DateOfBirth') || null,
                    Phone: data.get('Phone') || null,
                    SocialLinks: data.get('SocialLinks') || null
                };
                var result = await Api.put('/api/profiles/' + accountId, body);
                Ui.showAlert(flash, 'success', result.message || 'Đã cập nhật hồ sơ.');
                await Promise.all([loadProfile(), loadPublicProfile()]);
            } catch (error) {
                Ui.applyFieldErrors(form, error.fieldErrors);
                showError(flash, error, 'Không thể cập nhật hồ sơ.');
            }
        });

        var gameForm = document.getElementById('game-form');
        document.getElementById('game-id').addEventListener('change', updateGameAccountForm);
        gameForm.addEventListener('submit', async function (event) {
            event.preventDefault();
            Ui.clearFieldErrors(gameForm);
            var gameId = Number(document.getElementById('game-id').value);
            if (!gameId) {
                Ui.applyFieldErrors(gameForm, { GameId: 'Vui lòng chọn tựa game.' });
                return;
            }
            try {
                var result = await Api.put('/api/profiles/' + accountId + '/games/' + gameId, {
                    InGameName: document.getElementById('in-game-name').value
                });
                Ui.showAlert(flash, 'success', result.message || 'Đã lưu tài khoản game.');
                await loadGames();
                await loadPublicProfile();
            } catch (error) {
                Ui.applyFieldErrors(gameForm, error.fieldErrors);
                showError(flash, error, 'Không thể lưu tài khoản game.');
            }
        });

        var identityForm = document.getElementById('identity-form');
        identityForm.addEventListener('submit', async function (event) {
            event.preventDefault();
            Ui.clearFieldErrors(identityForm);
            var file = document.getElementById('identity-file').files[0];
            if (!file) {
                Ui.applyFieldErrors(identityForm, { file: 'Chọn tệp cần tải lên.' });
                return;
            }
            var allowedExtensions = ['.jpg', '.jpeg', '.png', '.pdf'];
            var extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
            if (!allowedExtensions.includes(extension)) {
                Ui.applyFieldErrors(identityForm, { file: 'Chỉ chấp nhận tệp JPG, PNG hoặc PDF.' });
                return;
            }
            if (file.size === 0 || file.size > 10 * 1024 * 1024) {
                Ui.applyFieldErrors(identityForm, { file: 'Tệp phải có dung lượng lớn hơn 0 và không vượt quá 10 MB.' });
                return;
            }
            var body = new FormData();
            body.append('file', file);
            try {
                var result = await Api.postForm('/api/profiles/' + accountId + '/identity-documents', body);
                Ui.showAlert(flash, 'success', result.message || 'Đã tải giấy tờ lên.');
                identityForm.reset();
                await loadIdentityDocuments();
            } catch (error) {
                Ui.applyFieldErrors(identityForm, error.fieldErrors);
                showError(flash, error, 'Không thể tải giấy tờ lên.');
            }
        });

        var preferencesForm = document.getElementById('preferences-form');
        preferencesForm.addEventListener('submit', async function (event) {
            event.preventDefault();
            try {
                var result = await Api.put('/api/profiles/' + accountId + '/notification-preferences', {
                    NotifyInApp: document.querySelector('[name="NotifyInApp"]').checked,
                    NotifyEmail: document.querySelector('[name="NotifyEmail"]').checked
                });
                Ui.showAlert(flash, 'success', result.message || 'Đã lưu tùy chọn thông báo.');
            } catch (error) {
                showError(flash, error, 'Không thể lưu tùy chọn.');
            }
        });
    }

    function wireSectionNavigation() {
        var links = Array.from(document.querySelectorAll('.profile-section-nav a'));
        var sections = links.map(function (link) {
            return document.querySelector(link.getAttribute('href'));
        }).filter(Boolean);
        if (!('IntersectionObserver' in window)) return;

        var observer = new IntersectionObserver(function (entries) {
            var visible = entries.filter(function (entry) { return entry.isIntersecting; })
                .sort(function (left, right) { return left.boundingClientRect.top - right.boundingClientRect.top; })[0];
            if (!visible) return;
            links.forEach(function (link) {
                var active = link.getAttribute('href') === '#' + visible.target.id;
                link.classList.toggle('active', active);
                if (active) link.setAttribute('aria-current', 'location');
                else link.removeAttribute('aria-current');
            });
        }, { rootMargin: '-18% 0px -68% 0px' });

        sections.forEach(function (section) { observer.observe(section); });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        wireSectionNavigation();
        flash = document.getElementById('profile-flash');
        var me = await Ui.requireAuth();
        if (!me) return;
        accountId = Number(me.userId);
        if (!accountId) {
            showError(flash, null, 'Không xác định được tài khoản đang đăng nhập.');
            return;
        }
        wireForms();
        var sections = [
            loadProfile,
            loadGames,
            loadIdentityDocuments,
            loadPreferences,
            loadPublicProfile,
            loadMatchHistory
        ];
        var results = await Promise.allSettled(sections.map(function (load) { return load(); }));
        var failures = results.filter(function (result) { return result.status === 'rejected'; });
        if (failures.length) {
            showError(flash, failures[0].reason, 'Một số thông tin hồ sơ không tải được.');
        }
    });
})();
