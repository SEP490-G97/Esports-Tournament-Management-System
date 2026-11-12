(function () {
    var tournamentId = Number(new URLSearchParams(location.search).get('tournamentId'));
    var root = document.getElementById('report-content');
    var flash = document.getElementById('report-flash');

    function text(value) {
        return value === null || value === undefined || value === ''
            ? '—'
            : Ui.escapeHtml(String(value));
    }

    function section(title, headers, rows) {
        return '<section class="i3-surface mb-3"><h2>' + text(title) + '</h2><div class="i3-table-wrap">' +
            '<table class="i3-table"><thead><tr>' + headers.map(function (header) {
                return '<th>' + text(header) + '</th>';
            }).join('') + '</tr></thead><tbody>' +
            (rows.length ? rows.map(function (row) {
                return '<tr>' + row.map(function (cell) { return '<td>' + text(cell) + '</td>'; }).join('') + '</tr>';
            }).join('') : '<tr><td colspan="' + headers.length + '">Không có dữ liệu.</td></tr>') +
            '</tbody></table></div></section>';
    }

    document.addEventListener('DOMContentLoaded', async function () {
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
                ' · Tạo lúc ' + new Date(report.generatedAt).toLocaleString('vi-VN');
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
                            item.note, item.isCurrent ? 'Có' : 'Không', new Date(item.recordedAt).toLocaleString('vi-VN')];
                    })) +
                section('Lịch và kết quả trận đấu', ['Mã trận', 'Đội A', 'Đội B', 'Tỷ số', 'Đội thắng', 'Trạng thái', 'Lịch'],
                    report.matches.map(function (item) {
                        var score = item.scoreA === null || item.scoreB === null
                            ? '—'
                            : item.scoreA + ' – ' + item.scoreB;
                        return [item.matchId, item.teamA, item.teamB, score,
                            item.winner, item.status, item.scheduledAt ? new Date(item.scheduledAt).toLocaleString('vi-VN') : '—'];
                    })) +
                section('Tranh chấp', ['Mã trận', 'Lý do', 'Trạng thái', 'Phán quyết', 'Khiếu nại', 'Mở lúc', 'Xử lý lúc'],
                    report.disputes.map(function (item) {
                        return [item.matchId, item.openReason, item.status, item.rulingReason, item.appealStatus,
                            new Date(item.openedAt).toLocaleString('vi-VN'),
                            item.resolvedAt ? new Date(item.resolvedAt).toLocaleString('vi-VN') : '—'];
                    })) +
                section('Bảng xếp hạng vòng bảng', ['Bảng', 'Đội', 'Đã đấu', 'Thắng', 'Thua', 'Hiệu số', 'Điểm'],
                    report.standings.map(function (item) {
                        return [item.groupLabel, item.teamName, item.played, item.won, item.lost, item.scoreDiff, item.points];
                    }));
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tạo bộ hồ sơ tổng kết.');
        }
    });
})();
