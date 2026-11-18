// POR-03 / UC-36 bước 5 — tab "Tổng quan" của giải: mô tả, thể lệ, mốc thời gian, lệ phí, cơ cấu
// giải thưởng, thể thức, đơn vị tổ chức. Khách xem được; giải Nháp chỉ Ban tổ chức của giải xem trước.
(function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };

    function fmtDateTime(iso) {
        if (!iso) return '—';
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? '—' : d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    function row(label, value) {
        return '<div class="ov-row"><span class="k">' + esc(label) + '</span><span class="v">' + value + '</span></div>';
    }

    function timeline(t) {
        var now = Date.now();
        var steps = [
            ['Mở đăng ký', t.regOpenAt], ['Đóng đăng ký', t.regCloseAt], ['Khai mạc', t.startAt], ['Kết thúc', t.endAt]
        ];
        return '<ol class="ov-timeline">' + steps.map(function (s) {
            var at = s[1] ? new Date(/Z$/.test(s[1]) ? s[1] : s[1] + 'Z').getTime() : null;
            var done = at !== null && at <= now;
            return '<li class="' + (done ? 'done' : '') + '"><span class="dot"></span><span class="lbl">' + esc(s[0]) + '</span>' +
                '<span class="at">' + esc(s[1] ? fmtDateTime(s[1]) : 'Chưa xác định') + '</span></li>';
        }).join('') + '</ol>';
    }

    function render(t) {
        var id = t.tournamentId;
        document.title = t.name + ' · ETOMS';
        document.getElementById('page-title').textContent = t.name;
        document.getElementById('page-sub').innerHTML = esc(t.game) + ' · ' + esc(t.formatLabel) +
            ' <span class="badge ' + esc(t.statusClass) + ' ms-1">' + esc(t.statusLabel) + '</span>';

        var notices = '';
        if (t.status === 'DRAFT') notices += '<div class="pending-banner mb-3">Bản xem trước cho Ban tổ chức: giải còn là bản nháp nên người khác chưa xem được.</div>';
        if (t.liveStreams > 0) notices += '<a class="live-banner mb-3" href="/tournament/live.html?id=' + id + '">' +
            '<span class="badge badge-live"><span class="live-dot"></span>Đang phát</span>' +
            '<span class="grow">Giải đang có ' + Number(t.liveStreams) + ' kênh phát trực tiếp.</span><span class="fw-bold">Xem ngay →</span></a>';
        if (t.status === 'CANCELLED') notices += '<div class="alert alert-danger mb-3"><strong>Giải đã bị hủy.</strong> Lý do: ' + esc(t.cancelReason || 'Không ghi lý do') + '</div>';

        // ===== Khối đầu trang: một câu trạng thái + MỘT hành động chính theo giai đoạn =====
        var full = t.approvedTeams >= t.maxTeams;
        var now = Date.now();
        var at = function (iso) { return iso ? new Date(/Z$/.test(iso) ? iso : iso + 'Z').getTime() : null; };
        var heroText, heroSub = '', primary = '', secondary = '';
        if (t.status === 'CANCELLED') {
            heroText = 'Giải đã bị hủy';
        } else if (t.registrationOpen) {
            heroText = 'Đang nhận đăng ký đến ' + fmtDateTime(t.regCloseAt);
            heroSub = full ? 'Đã đủ ' + t.maxTeams + ' suất — đơn mới vào danh sách chờ.' : 'Còn ' + (t.maxTeams - t.approvedTeams) + ' trên ' + t.maxTeams + ' suất.';
            primary = '<a class="btn btn-primary" data-hide-roles="ORGANIZER ADMIN" href="/registration/register.html?tournamentId=' + id + '">Đăng ký cho đội</a>';
            secondary = '<a class="btn btn-ghost" href="/tournament/rulebook-history.html?id=' + id + '">Đọc thể lệ</a>';
        } else if (t.status === 'PUBLISHED' && at(t.regOpenAt) > now) {
            heroText = 'Mở đăng ký từ ' + fmtDateTime(t.regOpenAt);
            heroSub = 'Khai mạc ' + fmtDateTime(t.startAt) + '.';
            secondary = '<a class="btn btn-ghost" href="/tournament/rulebook-history.html?id=' + id + '">Đọc thể lệ</a>';
        } else if (t.status === 'ONGOING') {
            heroText = 'Giải đang diễn ra';
            heroSub = t.approvedTeams ? t.approvedTeams + ' đội tham dự.' : 'Đang cập nhật thông tin đội.';
            primary = '<a class="btn btn-primary" href="/tournament/bracket.html?id=' + id + '">Xem nhánh đấu &amp; kết quả</a>';
            if (t.liveStreams > 0) secondary = '<a class="btn btn-ghost" href="/tournament/live.html?id=' + id + '">Xem trực tiếp</a>';
        } else if (t.status === 'COMPLETED') {
            heroText = 'Giải đã kết thúc';
            primary = '<a class="btn btn-primary" href="/tournament/bracket.html?id=' + id + '">Xem kết quả</a>';
            secondary = '<a class="btn btn-ghost" href="/tournament/standings.html?id=' + id + '">Bảng xếp hạng</a>';
        } else if (t.status === 'DRAFT') {
            heroText = 'Bản nháp';
        } else {
            heroText = 'Đã đóng đăng ký — chờ khai mạc ' + fmtDateTime(t.startAt);
            primary = '<a class="btn btn-primary" href="/tournament/participants.html?id=' + id + '">Xem đội tham dự</a>';
        }
        var cta = '<section class="card-etoms ov-hero mb-3"><div class="body">' +
            '<div class="ov-hero-top"><div class="minw-0"><div class="ov-hero-title">' + esc(heroText) + '</div>' +
            (heroSub ? '<div class="hint">' + esc(heroSub) + '</div>' : '') + '</div>' +
            '<div class="ov-hero-actions">' + secondary + primary + '</div></div>' +
            '<div class="ov-facts">' +
            fact('Tựa game', esc(t.game)) +
            fact('Thể thức', esc(t.formatLabel)) +
            fact('Đội tham dự', t.approvedTeams + '<small>/' + t.maxTeams + '</small>') +
            fact('Lệ phí', t.entryFee > 0 ? Number(t.entryFee).toLocaleString('vi-VN') + 'đ' : 'Miễn phí') +
            fact('Khai mạc', esc(fmtDate(t.startAt))) +
            '</div></div></section>';

        var format = esc(t.formatLabel);
        if (t.groupCount) format += ' · ' + t.groupCount + ' bảng' + (t.advancePerGroup ? ', mỗi bảng ' + t.advancePerGroup + ' đội đi tiếp' : '');
        if (t.hasThirdPlaceMatch) format += ' · có trận tranh hạng Ba';

        var place = t.isOnline ? 'Trực tuyến' : esc(t.venue || 'Chưa công bố địa điểm');
        var requirements = [
            [t.requireInGameName, 'Khai báo tên trong game (IGN) cho từng thành viên'],
            [t.requireIdentityDocument, 'Giấy tờ định danh (thẻ sinh viên/CCCD)'],
            [t.requirePaymentProof && t.entryFee > 0, 'Biên lai chuyển khoản lệ phí']
        ].filter(function (r) { return r[0]; });

        // Thứ tự đọc: đầu trang (trạng thái + hành động + 5 ô nhanh) -> tiến độ -> giới thiệu & giải thưởng
        // -> thông tin chi tiết (thu gọn). Đội tham dự / nhánh đấu / thể lệ / trực tiếp nằm ở thanh bên, không lặp lại.
        document.getElementById('overview-root').innerHTML = notices + cta +
            '<section class="card-etoms mb-3"><div class="body"><div class="ov-h">Tiến độ giải</div>' + timeline(t) + '</div></section>' +
            '<div class="row g-3">' +
            '<div class="col-12 col-lg-7"><section class="card-etoms h-100"><div class="body"><div class="ov-h">Giới thiệu</div>' +
            (t.description ? '<p class="ov-desc mb-0">' + esc(t.description) + '</p>' : '<p class="hint mb-0">Ban tổ chức chưa viết giới thiệu cho giải.</p>') +
            '</div></section></div>' +
            '<div class="col-12 col-lg-5"><section class="card-etoms h-100"><div class="body"><div class="ov-h">Giải thưởng</div>' +
            '<div class="ov-prize">' + (t.prize ? esc(t.prize) : '<span class="hint">Chưa công bố.</span>') + '</div>' +
            '</div></section></div>' +
            '</div>' +
            '<details class="card-etoms ov-more mt-3"><summary>Thông tin chi tiết</summary><div class="body pt-0">' +
            row('Đơn vị tổ chức', esc(t.organizingUnit || 'Chưa ghi')) +
            row('Thể thức', format) +
            row('Đội hình mỗi đội', t.starterCount + ' chính thức' + (t.maxSubstitutes ? ', tối đa ' + t.maxSubstitutes + ' dự bị' : '')) +
            row('Hình thức', place + (t.region ? ' · ' + esc(t.region) : '')) +
            row('Lệ phí', t.entryFee > 0 ? Number(t.entryFee).toLocaleString('vi-VN') + 'đ / đội' : 'Miễn phí') +
            row('Hồ sơ cần nộp', requirements.length ? requirements.map(function (r) { return esc(r[1]); }).join('; ') : 'Không yêu cầu thêm') +
            (t.contactInfo ? row('Liên hệ', esc(t.contactInfo)) : '') +
            row('Thể lệ', t.hasRulebook ? '<a href="/tournament/rulebook-history.html?id=' + id + '">Phiên bản ' + t.rulebookVersion + '</a>' : 'Chưa công bố') +
            '</div></details>';
    }

    function fact(label, value) {
        return '<div class="ov-fact"><span class="k">' + esc(label) + '</span><span class="v">' + value + '</span></div>';
    }

    function fmtDate(iso) {
        if (!iso) return 'Chưa xác định';
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? 'Chưa xác định' : d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var id = Ui.qsInt('id');
        var root = document.getElementById('overview-root');
        if (!id || id < 1) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã giải</h3><p>Hãy mở giải từ <a href="/tournament/index.html">danh sách giải</a>.</p></div></div>';
            return;
        }
        try {
            var res = await Api.get('/api/tournaments/' + id + '/overview');
            render(res.data);
        } catch (err) {
            // UC-36 E2: giải không tồn tại hoặc còn Nháp -> báo không tìm thấy, gợi ý về danh sách.
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>' +
                (err.status === 404 ? 'Không tìm thấy giải đấu' : 'Không tải được thông tin giải') + '</h3><p>' +
                Ui.escapeHtml(err.message || '') + '</p><a class="btn btn-primary mt-3" href="/tournament/index.html">Về danh sách giải</a></div></div>';
        }
    });
})();
