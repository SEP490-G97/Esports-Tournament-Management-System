// UC-46 — tab "Trực tiếp" của giải: ai cũng xem được (Guest). Trình phát nhúng của livestream đang chọn,
// thông tin trận và tỉ số hiện tại (UC-45), danh sách các livestream khác. Tự làm mới khi trang đang mở.
(function () {
    var REFRESH_MS = 15000;
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var tournamentId, data = null, selectedId = null, matchVersion = null, timer = null, shownPlayerKey = null;

    function selected() {
        return data.streams.find(function (s) { return s.livestreamId === selectedId; }) || data.streams[0];
    }

    // Chia nhóm Đang phát / Sắp phát / Đã kết thúc; bấm một kênh là đổi trình phát ngay trong trang.
    function listHtml() {
        var current = selected().livestreamId;
        return [['LIVE', 'Đang phát'], ['SCHEDULED', 'Sắp phát'], ['ENDED', 'Đã kết thúc (xem lại)']].map(function (g) {
            var items = data.streams.filter(function (s) { return s.status === g[0]; });
            if (!items.length) return '';
            return '<div class="live-group-title">' + esc(g[1]) + ' (' + items.length + ')</div>' + items.map(function (s) {
                return '<button type="button" class="live-item' + (s.livestreamId === current ? ' active' : '') + '" data-stream="' + s.livestreamId + '"' +
                    (s.livestreamId === current ? ' aria-current="true"' : '') + '>' +
                    '<span class="min-w-0"><span class="live-item-title">' + esc(s.title || s.matchLabel || 'Livestream') + '</span>' +
                    '<span class="hint d-block text-truncate">' + esc(s.platformLabel) + (s.matchLabel && s.title ? ' · ' + esc(s.matchLabel) : '') + '</span></span>' +
                    LivePlayer.streamBadge(s) + '</button>';
            }).join('');
        }).join('');
    }

    function render() {
        var root = document.getElementById('live-page-root');
        // Livestream đang xem (hoặc ?stream= trên link chia sẻ) vừa bị Ban tổ chức gỡ: báo rõ thay vì lặng lẽ đổi kênh.
        var removed = selectedId != null && !data.streams.some(function (s) { return s.livestreamId === selectedId; });
        if (removed) {
            selectedId = null;
            var u = new URL(location.href); u.searchParams.delete('stream'); history.replaceState(null, '', u);
            Ui.showAlert(document.getElementById('flash'), 'warning', 'Livestream bạn đang xem đã bị Ban tổ chức gỡ hoặc đã kết thúc.' +
                (data.streams.length ? ' Đang chuyển sang livestream khác của giải.' : ''));
        }
        if (!data.streams.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Chưa có livestream</h3>' +
                '<p>' + (removed ? 'Livestream vừa bị gỡ và giải hiện không còn kênh phát nào.' : 'Ban tổ chức chưa thêm link phát trực tiếp cho giải này.') +
                ' Bạn vẫn có thể theo dõi tỉ số ở tab Nhánh đấu.</p>' +
                (data.canManage ? '<a class="btn btn-primary" href="/tournament/livestreams.html?id=' + tournamentId + '">Thêm livestream</a>' : '') +
                '</div></div>';
            shownPlayerKey = null;
            return;
        }
        var s = selected();
        selectedId = s.livestreamId;
        var playerKey = s.livestreamId + ':' + s.streamUrl + ':' + s.status;
        if (!document.getElementById('live-player-box')) {
            // Video chiếm cột chính (rộng nhất có thể); tỉ số + danh sách livestream gom sang cột phụ bên phải,
            // màn hẹp thì xếp xuống dưới video.
            root.innerHTML = '<div class="live-layout">' +
                '<section class="card-etoms live-stage"><div class="body" id="live-player-box"></div></section>' +
                '<aside class="live-aside">' +
                '<section class="card-etoms" id="live-match-card" hidden><div class="body">' +
                '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2"><div class="section-title m-0">Tỉ số trận đấu</div>' +
                '<a class="btn btn-ghost btn-sm" id="live-match-link" href="#">Trang trận</a></div>' +
                '<div id="live-match-body" class="mt-2"></div></div></section>' +
                '<section class="card-etoms"><div class="body">' +
                '<div class="section-title mt-0">Các livestream (<span id="live-count"></span>)</div>' +
                '<div class="live-list" id="live-list"></div>' +
                (data.canManage ? '<a class="btn btn-ghost w-100 mt-3" href="/tournament/livestreams.html?id=' + tournamentId + '">Quản lý livestream</a>' : '') +
                '</div></section></aside></div>';
            if (!root.dataset.wired) root.addEventListener('click', function (e) {
                var b = e.target.closest('[data-stream]');
                if (!b) return;
                selectedId = parseInt(b.dataset.stream, 10);
                matchVersion = null;
                render();
                // Giữ kênh đang xem trên URL (chia sẻ được); màn hẹp danh sách nằm dưới video nên cuộn lên trình phát.
                var u = new URL(location.href); u.searchParams.set("stream", String(selectedId)); history.replaceState(null, "", u);
                if (window.matchMedia("(max-width: 1199.98px)").matches) document.getElementById("live-player-box").scrollIntoView({ behavior: "smooth", block: "start" });
            });
        }
        root.dataset.wired = '1';
        document.getElementById('live-count').textContent = data.streams.length;
        document.getElementById('live-list').innerHTML = listHtml();
        // Chỉ nạp lại trình phát khi đổi livestream — tránh video bị tải lại mỗi lần làm mới.
        if (playerKey !== shownPlayerKey) {
            document.getElementById('live-player-box').innerHTML = LivePlayer.playerHtml(s);
            shownPlayerKey = playerKey;
        }
        loadMatch(s);
    }

    async function loadMatch(s) {
        var card = document.getElementById('live-match-card');
        if (!s.matchId) { card.hidden = true; return; }
        try {
            var m = (await Api.get('/api/matches/' + s.matchId + '/live')).data;
            if (selected().matchId !== m.matchId) return; // người xem đã chọn livestream khác
            card.hidden = false;
            document.getElementById('live-match-link').href = '/match/detail.html?id=' + m.matchId;
            if (m.version !== matchVersion) {
                document.getElementById('live-match-body').innerHTML = LivePlayer.scoreHtml(m);
                matchVersion = m.version;
            }
        } catch (e) { card.hidden = true; }
    }

    async function load(first) {
        try {
            data = (await Api.get('/api/tournaments/' + tournamentId + '/livestreams')).data;
            if (first) {
                document.getElementById('page-title').textContent = 'Trực tiếp · ' + data.tournamentName;
                document.title = 'Trực tiếp · ' + data.tournamentName + ' · ETOMS';
            }
            render();
        } catch (err) {
            if (!first) return;
            document.getElementById('live-page-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>' +
                (err.status === 404 ? 'Không tìm thấy giải đấu' : 'Không tải được livestream') + '</h3><p>' + esc(err.message || '') +
                '</p><a class="btn btn-primary mt-3" href="/tournament/index.html">Về danh sách giải</a></div></div>';
            return false;
        }
        return true;
    }

    function schedule() {
        clearTimeout(timer);
        timer = setTimeout(async function () {
            if (document.visibilityState === 'visible') await load(false);
            schedule();
        }, REFRESH_MS);
    }

    document.addEventListener('DOMContentLoaded', async function () {
        tournamentId = Ui.qsInt('id');
        selectedId = Ui.qsInt('stream'); // mở đúng kênh khi đến từ trang chủ (khu "Đang phát trực tiếp")
        if (!tournamentId || tournamentId < 1) {
            document.getElementById('live-page-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã giải</h3>' +
                '<p>Hãy mở giải từ <a href="/tournament/index.html">danh sách giải</a>.</p></div></div>';
            return;
        }
        if (await load(true)) schedule();
    });
})();
