(function () {
    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDateShort(iso) {
        var d = new Date(iso);
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + ' ' + pad2(d.getHours()) + ':' + pad2(d.getMinutes());
    }
    function reasonLabel(r) {
        return r === 'FLAGGED' ? 'Bị cắm cờ' : r === 'CONFLICTING_REPORT' ? 'Báo cáo lệch nhau' : 'Hết hạn báo cáo';
    }

    function render(list) {
        var root = document.getElementById('disputes-root');
        if (!list.length) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không có tranh chấp nào</h3><p>Hàng đợi của bạn đang trống.</p></div></div>';
            return;
        }
        var html = '<div class="d-grid gap-3">';
        list.forEach(function (d) {
            html += '<a href="/result/dispute.html?id=' + d.disputeId + '" class="card-etoms d-block" style="color:inherit">' +
                '<div class="body d-flex flex-wrap justify-content-between align-items-center gap-3" style="padding:18px 22px">' +
                '<div><div style="font-weight:800">' + Ui.escapeHtml(d.teamAName) + ' vs ' + Ui.escapeHtml(d.teamBName) + '</div>' +
                '<div style="color:var(--muted);font-size:.86rem;margin-top:2px">' + Ui.escapeHtml(d.tournamentName) + '</div></div>' +
                '<div class="d-flex flex-wrap gap-2 align-items-center">' +
                '<span class="badge ' + (d.openReason === 'FLAGGED' ? 'badge-danger' : 'badge-soft') + '">' + reasonLabel(d.openReason) + '</span>' +
                '<span class="badge ' + (d.status === 'UNDER_REVIEW' ? 'badge-active' : 'badge-gold') + '">' + (d.status === 'UNDER_REVIEW' ? 'Đang xem xét' : 'Mới mở') + '</span>' +
                '<span style="color:var(--muted-2);font-size:.82rem">' + fmtDateShort(d.openedAt) + '</span>' +
                '</div></div></a>';
        });
        html += '</div>';
        root.innerHTML = html;
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var root = document.getElementById('disputes-root');
        var me = await Ui.requireAuth();
        if (!me) return;
        try {
            var list = await Api.get('/api/disputes');
            render(list);
        } catch (err) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    });
})();
