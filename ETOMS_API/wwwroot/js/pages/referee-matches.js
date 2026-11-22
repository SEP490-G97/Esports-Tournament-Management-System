// Trận tôi làm trọng tài — API /api/referee/matches (góp ý 22/11).
(function () {
    var el = function (id) { return document.getElementById(id); };
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };

    function fmt(iso) {
        if (!iso) return 'Chưa xếp lịch';
        var d = new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z');
        return d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    function render(rows) {
        if (!rows.length) {
            el('list').innerHTML = '<div class="empty"><h3>Bạn chưa được gán trọng tài trận nào</h3>' +
                '<p>Chủ nhiệm đơn vị phân vai trò Trọng tài cho bạn, sau đó Ban tổ chức giải gán bạn vào trận ở trang Trọng tài của giải.</p></div>';
            return;
        }
        el('list').innerHTML = '<div class="table-wrap"><table class="data-table"><thead><tr><th>Trận</th><th>Giải</th><th>Lịch</th><th>Trạng thái</th><th class="text-end">Việc cần làm</th></tr></thead><tbody>' +
            rows.map(function (m) {
                var score = m.scoreA != null && m.scoreB != null ? ' <b>' + m.scoreA + ' – ' + m.scoreB + '</b>' : '';
                var action = m.openDisputeId
                    ? '<a class="btn btn-primary btn-sm" href="/result/dispute.html?id=' + Number(m.openDisputeId) + '">Phán quyết tranh chấp</a>'
                    : '<a class="btn btn-ghost btn-sm" href="/match/detail.html?id=' + Number(m.matchId) + '">Mở trận</a>';
                return '<tr><td>' + esc(m.teamA) + ' vs ' + esc(m.teamB) + score + '<div class="field-hint">BO' + Number(m.bestOf) + '</div></td>' +
                    '<td><a href="/bracket/view.html?id=' + Number(m.tournamentId) + '">' + esc(m.tournamentName) + '</a></td>' +
                    '<td>' + esc(fmt(m.scheduledAt)) + '</td>' +
                    '<td><span class="badge ' + esc(m.statusClass) + '">' + esc(m.statusLabel) + '</span></td>' +
                    '<td class="text-end text-nowrap">' + action + '</td></tr>';
            }).join('') + '</tbody></table></div>';
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ORGANIZER']);
        if (!me) return;
        try { render(await Api.get('/api/referee/matches')); }
        catch (err) { Ui.showAlert(el('flash'), 'danger', err.message || 'Không tải được danh sách trận.'); }
    });
})();
