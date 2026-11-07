(function () {
    var tournamentId = null;

    function showEmpty(title, message) {
        document.getElementById('dashboard-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    // Server trả giờ UTC không kèm múi giờ.
    function formatTime(value) {
        if (!value) return '';
        var date = new Date(/Z$/.test(value) ? value : value + 'Z');
        return date.toLocaleString('vi-VN');
    }

    function formatMoney(value) {
        return Number(value).toLocaleString('vi-VN') + ' đ';
    }

    function statCard(label, value, sub, style) {
        return '<div class="col"><div class="stat-card h-100"><div class="label">' + Ui.escapeHtml(label) + '</div>' +
            '<div class="num"' + (style ? ' style="' + style + '"' : '') + '>' + Ui.escapeHtml(value) + '</div>' +
            '<div class="sub">' + Ui.escapeHtml(sub) + '</div></div></div>';
    }

    function statCards(d) {
        var checkIn = d.checkInMatchCount > 0
            ? statCard('Tiến độ check-in', d.checkedInTeamCount + '/' + d.expectedTeamCount, d.checkInMatchCount + ' trận đang mở check-in')
            : statCard('Tiến độ check-in', '—', 'Không có trận nào đang mở check-in');

        var fee = d.hasEntryFee
            ? statCard('Lệ phí đã xác nhận', formatMoney(d.confirmedFeeTotal),
                d.confirmedPaymentCount + ' đội, theo số đội khai' + (d.missingAmountCount > 0 ? ', ' + d.missingAmountCount + ' đội không khai số tiền' : ''),
                'font-size:1.5rem;margin-top:14px')
            : statCard('Lệ phí', 'Miễn phí', 'Giải không thu lệ phí', 'font-size:1.5rem;margin-top:14px');

        return '<div class="row row-cols-1 row-cols-sm-2 row-cols-lg-3 g-3">' +
            statCard('Đơn chờ duyệt', String(d.pendingCount), 'cần Ban tổ chức xử lý', d.pendingCount > 0 ? 'color:var(--warn)' : '') +
            statCard('Đội đã duyệt', d.approvedCount + '/' + d.maxTeams, 'còn ' + d.remainingSlots + ' suất') +
            statCard('Hàng đợi', String(d.waitlistedCount), 'đội trong danh sách chờ') +
            checkIn +
            statCard('Tranh chấp đang mở', String(d.openDisputeCount),
                d.pendingAppealCount > 0 ? d.pendingAppealCount + ' khiếu nại đang chờ' : 'không có khiếu nại đang chờ',
                d.openDisputeCount > 0 ? 'color:var(--danger)' : '') +
            fee +
            '</div>';
    }

    function countBadges(items, emptyText) {
        if (!items.length) return '<div style="color:var(--muted);font-size:.9rem">' + Ui.escapeHtml(emptyText) + '</div>';
        return items.map(function (c) {
            return '<span class="badge badge-soft" style="margin:0 6px 6px 0">' + Ui.escapeHtml(c.label) + ': ' + c.count + '</span>';
        }).join('');
    }

    function section(title, bodyHtml) {
        return '<div class="col-12 col-lg-6"><div class="card-etoms h-100"><div class="body">' +
            '<div class="section-title" style="margin:0 0 14px">' + Ui.escapeHtml(title) + '</div>' + bodyHtml + '</div></div></div>';
    }

    function listOrEmpty(items, rowFn, emptyText) {
        if (!items.length) return '<div style="color:var(--muted);font-size:.9rem">' + Ui.escapeHtml(emptyText) + '</div>';
        return items.map(rowFn).join('');
    }

    function row(name, detail, badgeHtml) {
        return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:10px;margin-bottom:10px">' +
            '<div class="grow"><div class="nm">' + Ui.escapeHtml(name) + '</div><div class="rl">' + Ui.escapeHtml(detail) + '</div></div>' +
            (badgeHtml || '') + '</div>';
    }

    function render(d) {
        document.getElementById('page-title').textContent = 'Bảng điều hành ' + d.tournamentName;
        document.getElementById('page-sub').innerHTML =
            'Trạng thái giải: <span class="badge ' + Ui.escapeHtml(d.statusClass) + '">' + Ui.escapeHtml(d.statusLabel) + '</span>';

        var todayRows = listOrEmpty(d.todayMatches, function (m) {
            return row(m.teamAName + ' vs ' + m.teamBName, formatTime(m.scheduledAt),
                '<span class="badge ' + Ui.escapeHtml(m.statusClass) + '">' + Ui.escapeHtml(m.statusLabel) + '</span>');
        }, 'Hôm nay không có trận nào.');

        var pendingRows = listOrEmpty(d.pendingRegistrations, function (r) {
            return row(r.teamName, r.submittedAt ? 'Nộp lúc ' + formatTime(r.submittedAt) : 'Chưa rõ thời điểm nộp');
        }, 'Không có đơn nào đang chờ duyệt.');

        var disputeRows = listOrEmpty(d.openDisputes, function (x) {
            return row(x.teamAName + ' vs ' + x.teamBName, x.reasonLabel + ' · ' + formatTime(x.openedAt),
                x.hasPendingAppeal ? '<span class="badge badge-gold">Có khiếu nại</span>' : '');
        }, 'Không có tranh chấp nào đang mở.');

        var overview =
            '<div style="font-weight:700;margin-bottom:8px">Đơn đăng ký</div>' + countBadges(d.registrationsByStatus, 'Chưa có đơn đăng ký.') +
            '<div style="font-weight:700;margin:14px 0 8px">Trận đấu (' + d.totalMatches + ')</div>' + countBadges(d.matchesByStatus, 'Chưa có trận đấu.');

        document.getElementById('dashboard-root').innerHTML =
            statCards(d) +
            '<div class="row g-3" style="margin-top:6px">' +
            section('Trận đấu hôm nay', todayRows) +
            section('Đơn chờ duyệt', pendingRows) +
            section('Tranh chấp đang mở', disputeRows) +
            section('Tổng quan', overview) +
            '</div>' +
            '<div class="btn-row" style="margin-top:22px">' +
            '<a class="btn btn-ghost" href="/tournament/manage.html?id=' + tournamentId + '">Quản lý giải</a>' +
            '<a class="btn btn-ghost" href="/tournament/rulebook.html?id=' + tournamentId + '">Thể lệ giải</a>' +
            '<a class="btn btn-ghost" href="/tournament/referees.html?id=' + tournamentId + '">Gán trọng tài</a>' +
            '</div>';
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        tournamentId = Ui.qsInt('id');
        if (tournamentId === null) {
            showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.');
            return;
        }

        try {
            render(await Api.get('/api/tournaments/' + tournamentId + '/dashboard'));
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể xem bảng điều hành này', 'Chỉ Ban tổ chức của giải được xem.');
            else showEmpty('Không tải được số liệu của giải', err.message);
        }
    });
})();
