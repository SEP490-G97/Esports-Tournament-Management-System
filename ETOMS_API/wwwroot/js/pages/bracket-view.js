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

        // Khối đăng ký: chỉ hiện khi đang mở; đủ suất thì báo đơn sẽ vào danh sách chờ.
        var full = t.approvedTeams >= t.maxTeams;
        var cta = t.registrationOpen
            ? '<div class="card-etoms ov-cta mb-3"><div class="body d-flex flex-wrap align-items-center justify-content-between gap-3">' +
              '<div><div class="fw-bold">Đang mở đăng ký đến ' + esc(fmtDateTime(t.regCloseAt)) + '</div>' +
              '<div class="hint">' + (full ? 'Đã đủ ' + t.maxTeams + ' suất — đơn mới sẽ vào danh sách chờ.' : 'Còn ' + (t.maxTeams - t.approvedTeams) + '/' + t.maxTeams + ' suất.') + '</div></div>' +
              '<a class="btn btn-primary" data-hide-roles="ORGANIZER ADMIN" href="/registration/register.html?tournamentId=' + id + '">Đăng ký cho đội</a>' +
              '</div></div>'
            : '';

        var format = esc(t.formatLabel);
        if (t.groupCount) format += ' · ' + t.groupCount + ' bảng' + (t.advancePerGroup ? ', mỗi bảng ' + t.advancePerGroup + ' đội đi tiếp' : '');
        if (t.hasThirdPlaceMatch) format += ' · có trận tranh hạng Ba';

        var place = t.isOnline ? 'Trực tuyến' : esc(t.venue || 'Chưa công bố địa điểm');
        var requirements = [
            [t.requireInGameName, 'Khai báo tên trong game (IGN) cho từng thành viên'],
            [t.requireIdentityDocument, 'Giấy tờ định danh (thẻ sinh viên/CCCD)'],
            [t.requirePaymentProof && t.entryFee > 0, 'Biên lai chuyển khoản lệ phí']
        ].filter(function (r) { return r[0]; });

        document.getElementById('overview-root').innerHTML = notices + cta +
            '<div class="row g-3">' +
            '<div class="col-12 col-lg-7 d-flex flex-column gap-3">' +
            '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Giới thiệu</div>' +
            (t.description ? '<p class="ov-desc">' + esc(t.description) + '</p>' : '<p class="hint mb-0">Ban tổ chức chưa viết mô tả cho giải.</p>') +
            '</div></section>' +
            '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Thông tin chung</div>' +
            row('Đơn vị tổ chức', esc(t.organizingUnit || 'Chưa ghi')) +
            row('Tựa game', esc(t.game)) +
            row('Thể thức', format) +
            row('Quy mô', t.maxTeams + ' đội · mỗi đội ' + t.starterCount + ' chính thức + tối đa ' + t.maxSubstitutes + ' dự bị') +
            row('Hình thức', place + (t.region ? ' · ' + esc(t.region) : '')) +
            (t.contactInfo ? row('Liên hệ', esc(t.contactInfo)) : '') +
            '</div></section>' +
            '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Yêu cầu hồ sơ đăng ký</div>' +
            (requirements.length
                ? '<ul class="ov-list">' + requirements.map(function (r) { return '<li>' + esc(r[1]) + '</li>'; }).join('') + '</ul>'
                : '<p class="hint mb-0">Không yêu cầu hồ sơ bổ sung.</p>') +
            '</div></section>' +
            '</div>' +
            '<div class="col-12 col-lg-5 d-flex flex-column gap-3">' +
            '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Mốc thời gian</div>' + timeline(t) + '</div></section>' +
            '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Lệ phí &amp; giải thưởng</div>' +
            row('Lệ phí tham gia', t.entryFee > 0 ? Number(t.entryFee).toLocaleString('vi-VN') + 'đ / đội' : 'Miễn phí') +
            '<div class="ov-prize">' + (t.prize ? esc(t.prize) : '<span class="hint">Ban tổ chức chưa công bố cơ cấu giải thưởng.</span>') + '</div>' +
            '</div></section>' +
            '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Xem thêm</div>' +
            '<div class="d-grid gap-2">' +
            '<a class="btn btn-ghost" href="/tournament/participants.html?id=' + id + '">Đội tham dự (' + t.approvedTeams + '/' + t.maxTeams +
            (t.waitlistedTeams ? ', ' + t.waitlistedTeams + ' chờ' : '') + ')</a>' +
            '<a class="btn btn-ghost" href="/tournament/bracket.html?id=' + id + '">Nhánh đấu, lịch thi đấu &amp; kết quả</a>' +
            '<a class="btn btn-ghost" href="/tournament/live.html?id=' + id + '">Xem trực tiếp (livestream &amp; tỉ số)</a>' +
            '<a class="btn btn-ghost" href="/tournament/rulebook-history.html?id=' + id + '">' +
            (t.hasRulebook ? 'Thể lệ giải (phiên bản ' + t.rulebookVersion + ')' : 'Thể lệ giải (chưa công bố)') + '</a>' +
            '</div></div></section>' +
            '</div></div>';
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
