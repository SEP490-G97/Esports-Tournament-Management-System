(function () {
    var tournamentId = Number(new URLSearchParams(location.search).get('tournamentId'));
    var root = document.getElementById('report-content');
    var flash = document.getElementById('report-flash');

    function text(value) {
        return value === null || value === undefined || value === ''
            ? '—'
            : Ui.escapeHtml(String(value));
    }

    // Bảng dài (giải 64–128 đội, hàng trăm trận): trên màn hình hiện 15 dòng/trang (góp ý 27/11), khi IN vẫn in đủ
    // mọi dòng — các dòng ngoài trang chỉ ẩn bằng lớp .rp-hide (CSS @media print hiện lại).
    var PAGE = 15;
    function section(title, headers, rows) {
        return '<section class="i3-surface mb-3" data-report-table><h2>' + text(title) + '</h2><div class="i3-table-wrap">' +
            '<table class="i3-table"><thead><tr>' + headers.map(function (header) {
                return '<th>' + text(header) + '</th>';
            }).join('') + '</tr></thead><tbody>' +
            (rows.length ? rows.map(function (row, i) {
                return '<tr' + (i >= PAGE ? ' class="rp-hide"' : '') + '>' + row.map(function (cell) { return '<td>' + text(cell) + '</td>'; }).join('') + '</tr>';
            }).join('') : '<tr><td colspan="' + headers.length + '">Không có dữ liệu.</td></tr>') +
            '</tbody></table></div><div class="rp-pager"></div></section>';
    }

    function wirePagers() {
        root.querySelectorAll('[data-report-table]').forEach(function (sec) {
            var rows = sec.querySelectorAll('tbody tr');
            var pages = Math.ceil(rows.length / PAGE);
            if (pages <= 1) return;
            var pager = sec.querySelector('.rp-pager');
            function show(p) {
                rows.forEach(function (r, i) { r.classList.toggle('rp-hide', i < (p - 1) * PAGE || i >= p * PAGE); });
                pager.innerHTML = '<div class="hint mt-2">' + rows.length + ' dòng · trang ' + p + '/' + pages + '</div>' + Ui.pagerHtml(p, pages);
            }
            pager.addEventListener('click', function (e) {
                var b = e.target.closest('[data-page]');
                if (b && !b.disabled) show(parseInt(b.dataset.page, 10));
            });
            show(1);
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        if (!Number.isInteger(tournamentId) || tournamentId <= 0) {
            Ui.showAlert(flash, 'danger', 'Mã giải đấu không hợp lệ.');
            return;
        }
        document.getElementById('report-excel').href =
            '/api/reports/tournaments/' + tournamentId + '/excel';
        document.getElementById('report-print').addEventListener('click', function () {
            window.print();
        });
        try {
            var response = await Api.get('/api/reports/tournaments/' + tournamentId);
            var report = response.data;
            document.title = 'Báo cáo ' + report.tournamentName + ' · ETOMS';
            document.getElementById('report-title').textContent = report.tournamentName;
            document.getElementById('report-subtitle').textContent =
                report.gameName + ' · Trạng thái ' + report.status +
                ' · Tạo lúc ' + Ui.fmtDateTime(report.generatedAt);
            root.innerHTML =
                '<section class="i3-surface mb-3"><h2>Tóm tắt</h2><p>Đội đăng ký: ' +
                report.registrations.length + ' · Trận đấu: ' + report.matches.length +
                ' · Tranh chấp: ' + report.disputes.length + '</p></section>' +
                section('Danh sách đăng ký', ['Mã đơn', 'Đội', 'Trạng thái', 'Lệ phí', 'Kết quả', 'Hạng'],
                    report.registrations.map(function (item) {
                        return [item.registrationId, item.teamName + ' [' + item.teamTag + ']',
                            item.status, item.paymentStatus, item.finalPlacement, item.finalRank];
                    })) +
                section('Lệ phí và minh chứng', ['Mã đơn', 'Đội', 'Loại', 'Số tiền', 'Ghi chú', 'Hiện hành', 'Thời gian'],
                    report.payments.map(function (item) {
                        return [item.registrationId, item.teamName, item.recordKind, item.declaredAmount,
                            item.note, item.isCurrent ? 'Có' : 'Không', Ui.fmtDateTime(item.recordedAt)];
                    })) +
                section('Lịch và kết quả trận đấu', ['Mã trận', 'Đội A', 'Đội B', 'Tỷ số', 'Đội thắng', 'Trạng thái', 'Lịch'],
                    report.matches.map(function (item) {
                        var score = item.scoreA === null || item.scoreB === null
                            ? '—'
                            : item.scoreA + ' – ' + item.scoreB;
                        return [item.matchId, item.teamA, item.teamB, score,
                            item.winner, item.status, item.scheduledAt ? Ui.fmtDateTime(item.scheduledAt) : '—'];
                    })) +
                section('Tranh chấp', ['Mã trận', 'Lý do', 'Trạng thái', 'Phán quyết', 'Khiếu nại', 'Mở lúc', 'Xử lý lúc'],
                    report.disputes.map(function (item) {
                        return [item.matchId, item.openReason, item.status, item.rulingReason, item.appealStatus,
                            Ui.fmtDateTime(item.openedAt),
                            item.resolvedAt ? Ui.fmtDateTime(item.resolvedAt) : '—'];
                    })) +
                section('Bảng xếp hạng vòng bảng', ['Bảng', 'Đội', 'Đã đấu', 'Thắng', 'Thua', 'Hiệu số', 'Điểm'],
                    report.standings.map(function (item) {
                        return [item.groupLabel, item.teamName, item.played, item.won, item.lost, item.scoreDiff, item.points];
                    }));
            wirePagers();
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tạo bộ hồ sơ tổng kết.');
        }
    });
})();
