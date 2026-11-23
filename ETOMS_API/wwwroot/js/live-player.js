// UC-45/46 — phần dùng chung giữa trang trận và tab "Trực tiếp" của giải:
// vẽ trình phát nhúng (ETOMS không lưu video, chỉ nhúng link YouTube/Facebook/Twitch) và bảng tỉ số trực tiếp.
window.LivePlayer = (function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var STREAM_BADGE = { LIVE: 'badge-live', SCHEDULED: 'badge-soft', ENDED: 'badge-done' };

    function embedSrc(s) {
        if (!s.embedUrl) return null;
        // Twitch bắt buộc khai tên miền trang nhúng.
        return s.platform === 'TWITCH' ? s.embedUrl + '&parent=' + encodeURIComponent(location.hostname) : s.embedUrl;
    }

    function streamBadge(s) {
        return '<span class="badge ' + (STREAM_BADGE[s.status] || 'badge-soft') + '">' + esc(s.statusLabel) + '</span>';
    }

    function playerHtml(s) {
        var src = embedSrc(s);
        var title = s.title || s.matchLabel || 'Livestream';
        var body = src
            ? '<div class="live-player"><iframe src="' + esc(src) + '" title="' + esc(title) + '" loading="lazy" ' +
              'allow="autoplay; encrypted-media; picture-in-picture; fullscreen" allowfullscreen ' +
              'referrerpolicy="strict-origin-when-cross-origin"></iframe></div>'
            : '<div class="live-player live-player-off"><div><div class="fw-bold mb-1">Không phát được ngay trên ETOMS</div>' +
              '<div class="hint mb-3">Link này không nhúng được (link kênh hoặc nền tảng khác). Hãy mở trên ' + esc(s.platformLabel) + '.</div></div></div>';
        return body +
            '<div class="live-player-meta"><div class="min-w-0"><div class="fw-bold text-truncate">' + esc(title) + '</div>' +
            '<div class="hint">' + esc(s.platformLabel) + (s.matchLabel ? ' · ' + esc(s.matchLabel) : '') + '</div></div>' +
            '<div class="d-flex align-items-center gap-2 flex-wrap">' + streamBadge(s) +
            '<a class="btn btn-ghost btn-sm" href="' + esc(s.streamUrl) + '" target="_blank" rel="noopener noreferrer">Mở trên ' + esc(s.platformLabel) + ' ↗</a></div></div>';
    }

    function scoreHtml(d) {
        var leader = d.seriesLeaderTeamId;
        function side(name, wins, teamId) {
            return '<div class="live-side' + (leader && leader === teamId ? ' win' : '') + '"><div class="nm">' + esc(name) + '</div><div class="num">' + Number(wins) + '</div></div>';
        }
        var final = d.status === 'VERIFIED' || d.status === 'WALKOVER';
        var rows = d.games.length
            ? '<table class="live-games"><thead><tr><th>Ván</th><th>' + esc(d.teamAName) + '</th><th>' + esc(d.teamBName) + '</th></tr></thead><tbody>' +
              d.games.map(function (g) {
                  return '<tr><td>' + g.gameNo + '</td><td class="' + (g.scoreA > g.scoreB ? 'w' : '') + '">' + esc(g.scoreA) + '</td>' +
                      '<td class="' + (g.scoreB > g.scoreA ? 'w' : '') + '">' + esc(g.scoreB) + '</td></tr>';
              }).join('') + '</tbody></table>'
            : (final ? '' : '<p class="hint mt-3 mb-0">Chưa có ván nào được cập nhật tỉ số.</p>');
        var badge = '<span class="badge ' + (d.status === 'LIVE' ? 'badge-live' : 'badge-soft') + '">' + esc(d.statusLabel) + '</span>';
        return '<div class="live-score">' + side(d.teamAName, d.winsA, d.teamAId) +
            '<div class="live-mid"><div class="hint">BO' + d.bestOf + '</div>' + badge + '</div>' +
            side(d.teamBName, d.winsB, d.teamBId) + '</div>' + rows +
            '<p class="hint mt-2 mb-0">' + (final ? 'Đây là kết quả chính thức của trận.' : 'Tỉ số trực tiếp do Trọng tài/Ban tổ chức cập nhật để theo dõi. Kết quả chính thức chốt khi hai đội trưởng nộp báo cáo khớp nhau.') + '</p>';
    }

    return { playerHtml: playerHtml, scoreHtml: scoreHtml, streamBadge: streamBadge };
})();
