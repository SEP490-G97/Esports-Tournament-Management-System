// Thống kê hệ thống (Admin) — API /api/admin/stats. Biểu đồ cột vẽ bằng HTML/CSS, một màu, số ghi ngay trên/cạnh cột.
(function () {
    var el = function (id) { return document.getElementById(id); };
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var num = function (n) { return Number(n || 0).toLocaleString('vi-VN'); };

    function tiles(t) {
        var list = [
            ['Tuyển thủ', t.players, 'Tài khoản vai trò Tuyển thủ'],
            ['Ban tổ chức', t.organizers, 'Tài khoản vai trò Ban tổ chức'],
            ['Tài khoản bị khóa', t.banned, 'Đang ở trạng thái khóa'],
            ['Đội đang hoạt động', t.teams, ''],
            ['Giải đấu', t.tournaments, num(t.ongoing) + ' giải đang diễn ra'],
            ['Đơn đã duyệt', t.approvedRegistrations, 'Đơn đăng ký giải được duyệt'],
            ['Trận đã xong', t.matchesCompleted, 'Trên tổng ' + num(t.matchesTotal) + ' trận']
        ];
        el('tiles').innerHTML = list.map(function (x) {
            return '<div class="stat-card"><div class="label">' + esc(x[0]) + '</div><div class="num">' + num(x[1]) + '</div>' +
                (x[2] ? '<div class="sub">' + esc(x[2]) + '</div>' : '') + '</div>';
        }).join('');
    }

    function hbars(id, rows) {
        if (!rows.length || rows.every(function (r) { return !r.count; })) { el(id).innerHTML = '<div class="empty"><p>Chưa có dữ liệu.</p></div>'; return; }
        var max = Math.max.apply(null, rows.map(function (r) { return r.count; })) || 1;
        el(id).innerHTML = rows.map(function (r) {
            var w = Math.round(r.count * 100 / max);
            return '<div class="hbar" title="' + esc(r.label) + ': ' + num(r.count) + '"><span class="lbl">' + esc(r.label) + '</span>' +
                '<span class="track"><span class="fill" style="width:calc(' + w + '% - 40px)"></span><span class="val">' + num(r.count) + '</span></span></div>';
        }).join('');
    }

    function vbars(id, subId, rows) {
        var total = rows.reduce(function (s, r) { return s + r.count; }, 0);
        el(subId).textContent = 'Tổng ' + num(total) + ' trong ' + rows.length + ' tháng.';
        var max = Math.max.apply(null, rows.map(function (r) { return r.count; })) || 1;
        el(id).innerHTML = '<div class="vbars" role="img" aria-label="' + esc(rows.map(function (r) { return 'Tháng ' + r.label + ': ' + r.count; }).join('; ')) + '">' +
            rows.map(function (r) {
                return '<div class="col" title="Tháng ' + esc(r.label) + ': ' + num(r.count) + '"><span class="val">' + num(r.count) + '</span>' +
                    '<span class="bar" style="height:' + Math.round(r.count * 85 / max) + '%"></span></div>';
            }).join('') + '</div>' +
            '<div class="vbars-x">' + rows.map(function (r) { return '<span>' + esc(r.label) + '</span>'; }).join('') + '</div>';
    }

    async function load() {
        try {
            var d = await Api.get('/api/admin/stats?months=' + el('months').value);
            tiles(d.tiles);
            hbars('c-status', d.tournamentsByStatus || []);
            hbars('c-games', d.topGames || []);
            vbars('c-accounts', 's-accounts', d.newAccounts || []);
            vbars('c-tournaments', 's-tournaments', d.newTournaments || []);
            vbars('c-registrations', 's-registrations', d.newRegistrations || []);
        } catch (err) { Ui.showAlert(el('flash'), 'danger', err.message || 'Không tải được thống kê.'); }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ADMIN']);
        if (!me) return;
        el('months').addEventListener('change', load);
        load();
    });
})();
