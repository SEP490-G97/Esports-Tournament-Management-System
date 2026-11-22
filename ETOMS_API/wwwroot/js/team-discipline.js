// Mục "Hồ sơ kỷ luật" ở trang hồ sơ đội (UC-34 hậu điều kiện, BR-PEN-01): mọi thẻ phạt của đội ở các giải,
// thẻ đã hủy vẫn hiện cùng dòng hủy và lý do hủy. Công khai, chỉ đọc.
window.TeamDiscipline = (function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };

    function when(iso) {
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? '' : d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    async function load(container, teamId) {
        if (!container) return;
        container.innerHTML = '<p class="hint">Đang tải hồ sơ kỷ luật...</p>';
        try {
            var list = ((await Api.get('/api/teams/' + teamId + '/penalties')) || {}).penalties || [];
            if (!list.length) { container.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Chưa có thẻ phạt</h3><p>Đội chưa bị cấp thẻ phạt ở giải nào.</p></div></div>'; return; }
            container.innerHTML = '<div class="d-grid gap-2">' + list.map(function (p) {
                var note = p.cancelsPenaltyId ? ' · hủy thẻ #' + p.cancelsPenaltyId
                    : p.cancelledByPenaltyId ? ' · đã hủy (dòng #' + p.cancelledByPenaltyId + ')' : '';
                return '<div class="member-card' + (p.status === 'CANCELLED' ? ' opacity-75' : '') + '">' +
                    '<div class="grow min-w-0"><div class="nm">#' + p.penaltyId + ' · ' + esc(p.severityLabel) + (p.points ? ' · ' + p.points + ' điểm' : '') + '</div>' +
                    '<div class="rl"><a href="/bracket/view.html?id=' + Number(p.tournamentId) + '">' + esc(p.tournamentName) + '</a> · ' + when(p.issuedAt) + ' · ' + esc(p.statusLabel) + note + '</div>' +
                    '<div>' + esc(p.reason) + '</div></div></div>';
            }).join('') + '</div>';
        } catch (e) {
            container.innerHTML = '<p class="hint">Không tải được hồ sơ kỷ luật của đội.</p>';
        }
    }

    return { load: load };
})();
