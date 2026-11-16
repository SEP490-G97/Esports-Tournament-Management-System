(function () {
    var accountId = Ui.qsInt('id');
    var flash = document.getElementById('public-profile-flash');

    function renderList(root, items, emptyText, renderer) {
        if (!items.length) {
            root.innerHTML = '<div class="empty"><p>' + Ui.escapeHtml(emptyText) + '</p></div>';
            return;
        }
        root.innerHTML = '<div class="feature-list">' + items.map(renderer).join('') + '</div>';
    }

    function renderProfile(profile) {
        document.title = profile.fullName + ' · Hồ sơ người chơi · ETOMS';
        document.getElementById('player-name').textContent = profile.fullName;
        var avatarUrl = Ui.safeUrl(profile.avatarUrl);
        var gameAccounts = (profile.gameAccounts || []).map(function (item) {
            return '<span class="badge badge-soft">' + Ui.escapeHtml(item.gameTitle) + ': ' + Ui.escapeHtml(item.inGameName) + '</span>';
        }).join(' ');
        var teams = (profile.teams || []).map(function (item) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(item.teamName) + '</div><div class="item-sub">' + Ui.escapeHtml(item.gameTitle) + '</div></div></div>';
        });
        var titles = (profile.titles || []).map(function (item) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(item.tournamentName) + '</div><div class="item-sub">' + Ui.escapeHtml(item.placementLabel || item.placement) + '</div></div></div>';
        });
        document.getElementById('public-player-profile').innerHTML =
            '<section class="feature-card"><div class="d-flex align-items-center gap-3 mb-4"><div class="org-mark">' +
            (avatarUrl ? '<img src="' + Ui.escapeHtml(avatarUrl) + '" alt="" />' : Ui.escapeHtml((profile.fullName || '?').slice(0, 1))) +
            '</div><div><h2 class="mb-1">' + Ui.escapeHtml(profile.fullName) + '</h2><div class="muted-copy">Người chơi ETOMS</div></div></div>' +
            '<h3 class="section-title">Tài khoản game</h3><div class="d-flex flex-wrap gap-2 mb-4">' +
            (gameAccounts || '<span class="muted-copy">Chưa có tài khoản game công khai.</span>') + '</div>' +
            '<div class="row g-3"><div class="col-md-6"><h3 class="section-title">Đội tuyển hiện tại</h3>' +
            (teams.length ? '<div class="feature-list">' + teams.join('') + '</div>' : '<p class="muted-copy">Chưa tham gia đội tuyển.</p>') +
            '</div><div class="col-md-6"><h3 class="section-title">Thành tích</h3>' +
            (titles.length ? '<div class="feature-list">' + titles.join('') + '</div>' : '<p class="muted-copy">Chưa có thành tích.</p>') +
            '</div></div></section>';
    }

    async function loadMatchHistory() {
        var response = await Api.get('/api/profiles/' + accountId + '/match-history');
        renderList(document.getElementById('public-match-history'), response.data || [], 'Chưa có lịch sử trận đấu.', function (match) {
            var score = match.scoreFor === null || match.scoreAgainst === null ? 'Chưa có kết quả' : match.scoreFor + ' - ' + match.scoreAgainst;
            var schedule = match.scheduledAt ? new Date(match.scheduledAt).toLocaleString('vi-VN') : 'Chưa xếp lịch';
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(match.tournamentName) + ' · ' + Ui.escapeHtml(match.teamName) + '</div><div class="item-sub">' + Ui.escapeHtml(match.slot) + ' · ' + Ui.escapeHtml(schedule) + '</div></div><span class="badge badge-soft">' + Ui.escapeHtml(match.statusLabel || match.status) + ' · ' + Ui.escapeHtml(score) + '</span></div>';
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        if (!accountId || accountId < 1) {
            Ui.showAlert(flash, 'danger', 'Liên kết hồ sơ người chơi không hợp lệ.');
            document.getElementById('public-player-profile').innerHTML = '';
            document.getElementById('public-match-history').innerHTML = '';
            return;
        }
        var results = await Promise.allSettled([
            Api.get('/api/profiles/' + accountId + '/public'),
            loadMatchHistory()
        ]);
        if (results[0].status === 'fulfilled') {
            renderProfile(results[0].value.data);
        } else {
            document.getElementById('public-player-profile').innerHTML = '';
            Ui.showAlert(flash, 'danger', results[0].reason.message || 'Không thể tải hồ sơ người chơi.');
        }
        if (results[1].status === 'rejected') {
            document.getElementById('public-match-history').innerHTML = '';
            Ui.showAlert(flash, 'danger', results[1].reason.message || 'Không thể tải lịch sử thi đấu.');
        }
    });
})();
