(function () {
    var accountId = Ui.qsInt('id');
    var flash = document.getElementById('public-profile-flash');
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };

    function renderList(root, items, emptyText, renderer) {
        if (!items.length) {
            root.innerHTML = '<div class="empty"><p>' + esc(emptyText) + '</p></div>';
            return;
        }
        // Lịch sử trận có thể rất dài: ô tìm kiếm + phân trang 10 mục/trang.
        Ui.listPager(root, {
            items: items, render: renderer, pageSize: 10, emptyText: emptyText,
            text: function (item) { return Object.keys(item).map(function (k) { var v = item[k]; return v !== null && typeof v !== 'object' ? v : ''; }).join(' '); },
            placeholder: 'Tìm theo tên giải, đội, đối thủ...'
        });
    }

    // Report 3 §3.3.9 "Thông số cốt lõi": số trận, tỉ lệ thắng, số giải, danh hiệu — chỉ từ kết quả đã xác nhận.
    function statsHtml(s) {
        if (!s) return '';
        var rate = s.winRate === null || s.winRate === undefined ? null : s.winRate;
        var tile = function (label, num, sub, extra) {
            return '<div class="stat-card pp-stat"><div class="label">' + label + '</div><div class="num">' + num + '</div>' +
                (extra || '') + '<div class="sub">' + sub + '</div></div>';
        };
        return '<section class="pp-stats" aria-label="Thông số cốt lõi">' +
            tile('Trận đã đấu', s.matchesPlayed,
                s.matchesPlayed ? '<span class="pp-win">' + s.wins + ' thắng</span> · <span class="pp-loss">' + s.losses + ' thua</span>' : 'Chưa có trận có kết quả') +
            tile('Tỉ lệ thắng', rate === null ? '—' : rate + '<small>%</small>',
                rate === null ? 'Cần ít nhất một trận có kết quả' : 'Trên ' + s.matchesPlayed + ' trận đã xác nhận',
                rate === null ? '' : '<div class="pp-bar" role="img" aria-label="Tỉ lệ thắng ' + rate + '%"><span style="width:' + rate + '%"></span></div>') +
            tile('Giải đã dự', s.tournamentsPlayed, s.tournamentsPlayed ? 'Đơn đăng ký được duyệt' : 'Chưa dự giải nào') +
            tile('Danh hiệu', s.championships, s.podiums ? s.podiums + ' lần vào top 3' : 'Chưa vào top 3') +
            '</section>' +
            '<p class="pp-note">Chỉ tính trận đã có kết quả xác nhận (hai đội báo cáo khớp, trọng tài phán quyết, hoặc xử thắng/thua). Trận đang tranh chấp hay chưa đá không được tính.</p>';
    }

    // 5 trận gần nhất đã có kết quả: chấm xanh = thắng, đỏ = thua.
    function formHtml(history) {
        var last = (history || []).filter(function (m) { return m.outcome; }).slice(0, 5);
        if (!last.length) return '';
        return '<div class="pp-form" title="' + last.length + ' trận gần nhất có kết quả, mới nhất bên trái (T = thắng, B = bại)" aria-label="Phong độ ' + last.length + ' trận gần nhất"><span>Phong độ</span>' + last.map(function (m) {
            var win = m.outcome === 'WIN';
            return '<b class="' + (win ? 'win' : 'loss') + '" title="' + esc((win ? 'Thắng ' : 'Thua ') + (m.opponentName || '')) + '">' + (win ? 'T' : 'B') + '</b>';
        }).join('') + '</div>';
    }

    function renderProfile(profile, history) {
        document.title = profile.fullName + ' · Hồ sơ người chơi · ETOMS';
        document.getElementById('player-name').textContent = profile.fullName;
        var avatarUrl = Ui.safeUrl(profile.avatarUrl);
        var gameAccounts = (profile.gameAccounts || []).map(function (item) {
            return '<span class="pp-ign"><small>' + esc(item.gameTitle) + '</small>' + esc(item.inGameName) + '</span>';
        }).join('');
        var teams = (profile.teams || []).map(function (item) {
            return '<a class="feature-list-item pp-link" href="/team/details.html?id=' + Number(item.teamId) + '"><div><div class="item-title">' + esc(item.teamName) +
                '</div><div class="item-sub">' + esc(item.gameTitle) + '</div></div><span class="pp-go" aria-hidden="true">→</span></a>';
        });
        var order = { CHAMPION: 0, RUNNER_UP: 1, THIRD: 2 };
        var titles = (profile.titles || []).slice().sort(function (a, b) {
            return (order[a.placement] ?? 9) - (order[b.placement] ?? 9);
        }).map(function (item) {
            var medal = item.placement === 'CHAMPION' ? 'gold' : item.placement === 'RUNNER_UP' ? 'silver' : item.placement === 'THIRD' ? 'bronze' : '';
            return '<a class="feature-list-item pp-link" href="/bracket/view.html?id=' + Number(item.tournamentId) + '"><div><div class="item-title">' + esc(item.tournamentName) +
                '</div><div class="item-sub">Thứ hạng chung cuộc</div></div><span class="pp-medal ' + medal + '">' + esc(item.placementLabel || item.placement) + '</span></a>';
        });
        var teamLine = (profile.teams || []).map(function (t) { return esc(t.teamName); }).join(', ');

        document.getElementById('public-player-profile').innerHTML =
            '<section class="feature-card pp-head">' +
            '<div class="pp-avatar">' + (avatarUrl ? '<img src="' + esc(avatarUrl) + '" alt="" />' : esc((profile.fullName || '?').slice(0, 1))) + '</div>' +
            '<div class="pp-id"><h2>' + esc(profile.fullName) + '</h2>' +
            '<div class="muted-copy">Người chơi ETOMS' + (teamLine ? ' · ' + teamLine : '') + '</div>' +
            '<div class="pp-igns">' + (gameAccounts || '<span class="muted-copy">Chưa có tài khoản game công khai.</span>') + '</div></div>' +
            formHtml(history) +
            '</section>' +
            statsHtml(profile.stats) +
            '<div class="row g-3 mt-1"><div class="col-md-6"><section class="feature-card h-100"><h3 class="section-title mt-0">Đội tuyển hiện tại</h3>' +
            (teams.length ? '<div class="feature-list">' + teams.join('') + '</div>' : '<p class="muted-copy mb-0">Chưa tham gia đội tuyển.</p>') +
            '</section></div><div class="col-md-6"><section class="feature-card h-100"><h3 class="section-title mt-0">Thành tích' +
            (titles.length > TITLE_LIMIT ? ' <span class="pp-count">' + titles.length + '</span>' : '') + '</h3>' +
            (titles.length ? '<div class="feature-list" id="pp-titles">' + titles.slice(0, TITLE_LIMIT).join('') + '</div>' +
                (titles.length > TITLE_LIMIT ? '<button type="button" class="btn btn-ghost btn-sm mt-2" id="pp-titles-more">Xem tất cả ' + titles.length + ' thành tích</button>' : '')
                : '<p class="muted-copy mb-0">Chưa có thành tích.</p>') +
            '</section></div></div>';

        // Nhiều thành tích: hiện 5 mục cao nhất (Vô địch trước), bấm để mở hết / thu gọn.
        var more = document.getElementById('pp-titles-more');
        if (more) {
            var open = false;
            more.addEventListener('click', function () {
                open = !open;
                document.getElementById('pp-titles').innerHTML = (open ? titles : titles.slice(0, TITLE_LIMIT)).join('');
                more.textContent = open ? 'Thu gọn' : 'Xem tất cả ' + titles.length + ' thành tích';
            });
        }
    }
    var TITLE_LIMIT = 5;

    function matchRow(match) {
        var when = match.scheduledAt ? Ui.fmtDateTime(match.scheduledAt) : 'Chưa xếp lịch';
        var title = match.opponentName
            ? esc(match.teamName) + ' <span class="pp-vs">vs</span> ' + esc(match.opponentName)
            : esc(match.teamName) + ' <span class="pp-vs">· chờ đối thủ</span>';
        var right;
        if (match.outcome) {
            var win = match.outcome === 'WIN';
            var score = match.scoreFor !== null && match.scoreAgainst !== null ? ' ' + match.scoreFor + ' - ' + match.scoreAgainst : '';
            right = '<span class="badge ' + (win ? 'badge-active' : 'badge-danger') + ' pp-result">' + (win ? 'Thắng' : 'Thua') + esc(score) + '</span>';
        } else {
            right = '<span class="badge badge-soft">' + esc(match.statusLabel || match.status) + '</span>';
        }
        return '<div class="feature-list-item pp-match' + (match.outcome ? ' is-' + match.outcome.toLowerCase() : '') + '">' +
            '<div class="grow"><div class="item-title">' + title + '</div>' +
            '<div class="item-sub">' + esc(match.tournamentName) + (match.gameTitle ? ' · ' + esc(match.gameTitle) : '') + ' · ' + esc(when) +
            (match.slot ? ' · ' + esc(match.slot === 'STARTER' ? 'Đá chính' : match.slot === 'SUBSTITUTE' ? 'Dự bị' : match.slot) : '') + '</div></div>' +
            right + '</div>';
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
            Api.get('/api/profiles/' + accountId + '/match-history')
        ]);
        var history = results[1].status === 'fulfilled' ? (results[1].value.data || []) : [];
        if (results[0].status === 'fulfilled') {
            renderProfile(results[0].value.data, history);
        } else {
            document.getElementById('public-player-profile').innerHTML = '';
            Ui.showAlert(flash, 'danger', results[0].reason.message || 'Không thể tải hồ sơ người chơi.');
        }
        if (results[1].status === 'fulfilled') {
            renderList(document.getElementById('public-match-history'), history, 'Chưa có lịch sử trận đấu.', matchRow);
        } else {
            document.getElementById('public-match-history').innerHTML = '';
            Ui.showAlert(flash, 'danger', results[1].reason.message || 'Không thể tải lịch sử thi đấu.');
        }
    });
})();
