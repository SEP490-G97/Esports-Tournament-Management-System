// Mục "Trận sắp tới" ở trang đội: lối vào trang trận (điểm danh MAT-02, tỉ số trực tiếp) và phòng chờ
// trận (MAT-07) — phòng chờ chỉ dành cho đội trưởng, trọng tài và Ban tổ chức (UC-25 E1).
window.TeamMatches = (function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };

    function when(iso) {
        if (!iso) return 'Chưa xếp giờ';
        var d = new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z');
        return d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    async function load(container, teamId, isCaptain) {
        if (!container) return;
        container.innerHTML = '<p class="hint">Đang tải lịch thi đấu...</p>';
        try {
            var list = (await Api.get('/api/teams/' + teamId + '/upcoming-matches')).data || [];
            if (!list.length) { container.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Chưa có trận sắp tới</h3><p>Trận sẽ hiện ở đây khi đội vào nhánh đấu của một giải.</p></div></div>'; return; }
            container.innerHTML = '<div class="d-grid gap-2">' + list.map(function (m) {
                var cta = isCaptain && m.lobbyOpen
                    ? '<a class="btn btn-primary btn-sm" href="/match/detail.html?id=' + Number(m.matchId) + '#lobby-box">Vào phòng chờ</a>'
                    : isCaptain && m.status === 'PENDING'
                        ? '<a class="btn btn-ghost btn-sm" href="/match/detail.html?id=' + Number(m.matchId) + '">Điểm danh</a>'
                        : '<a class="btn btn-ghost btn-sm" href="/match/detail.html?id=' + Number(m.matchId) + '">Xem trận</a>';
                return '<div class="member-card team-match-row">' +
                    '<div class="grow min-w-0"><div class="nm text-truncate">vs ' + esc(m.opponentName) + '</div>' +
                    '<div class="rl text-truncate">' + esc(m.tournamentName) + ' · ' + esc(when(m.scheduledAt)) + '</div></div>' +
                    '<span class="badge ' + (m.lobbyOpen ? 'badge-active' : 'badge-soft') + '">' + esc(m.statusLabel) + '</span>' + cta + '</div>';
            }).join('') + '</div>';
        } catch (e) {
            container.innerHTML = '<p class="hint">Không tải được lịch thi đấu của đội.</p>';
        }
    }

    return { load: load };
})();
