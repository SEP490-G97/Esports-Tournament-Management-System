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

    // Tiến độ theo TRẠNG THÁI THẬT của giải, không chỉ theo lịch: quá giờ khai mạc mà giải chưa đủ đội (chưa diễn ra)
    // thì không được tô "Khai mạc"/"Kết thúc" — trước đây thanh tiến độ chạy hết tới "Kết thúc" dù giải đứng yên.
    function timeline(t) {
        var now = Date.now(), st = t.status;
        var at = function (iso) { return iso ? new Date(/Z$/.test(iso) ? iso : iso + 'Z').getTime() : null; };
        var passed = function (iso) { var v = at(iso); return st !== 'DRAFT' && v !== null && v <= now; };
        var started = st === 'ONGOING' || st === 'COMPLETED';
        var steps = [
            ['Mở đăng ký', t.regOpenAt, started || passed(t.regOpenAt)],
            ['Đóng đăng ký', t.regCloseAt, started || passed(t.regCloseAt)],
            ['Khai mạc', t.startAt, started],
            ['Kết thúc', t.endAt, st === 'COMPLETED']
        ];
        // Bước đang bị chặn (tô đỏ + ghi chú): giải bị hủy, hoặc chưa đủ đội nên chưa khai mạc được.
        var blocked = -1, note = '';
        if (st === 'CANCELLED') {
            blocked = steps.findIndex(function (s) { return !s[2]; });
            note = 'Giải đã bị hủy';
        } else if (t.statusLabel === 'Chưa đủ đội') {
            blocked = 2;
            note = 'Chưa đủ đội nên chưa khai mạc';
        } else if (st === 'PUBLISHED' && passed(t.startAt)) {
            blocked = 2;
            note = 'Đang chờ Ban tổ chức bắt đầu giải';
        }
        return '<ol class="ov-timeline">' + steps.map(function (s, i) {
            var cls = s[2] ? 'done' : (i === blocked ? 'blocked' : '');
            return '<li class="' + cls + '"><span class="dot"></span><span class="lbl">' + esc(s[0]) +
                (i === blocked ? '<small class="ov-note">' + esc(note) + '</small>' : '') + '</span>' +
                '<span class="at">' + esc(s[1] ? fmtDateTime(s[1]) : 'Chưa xác định') + '</span></li>';
        }).join('') + '</ol>';
    }

    // Đơn bị từ chối / đã rút / hết hạn vẫn được nộp lại nên nút giữ sáng. API chỉ trả đơn của các đội mà người xem làm đội trưởng.
    // Cảnh báo trước khi rút đơn (28/11): lệ phí đã nộp không được hoàn; suất trống mời ngay đội đầu danh sách chờ,
    // đội đó được báo và phải nộp lệ phí mới vào giải.
    function withdrawWarning(r) {
        var fee = Number(r.entryFee) || 0;
        var paid = fee > 0 && (r.paymentStatus === 'PROOF_SUBMITTED' || r.paymentStatus === 'CONFIRMED');
        var money = fee.toLocaleString('vi-VN') + ' đ';
        if (r.status === 'DRAFT') return 'Hủy đơn nháp của đội ' + r.teamName + '? Đơn đã hủy không khôi phục được.';
        var head = r.status === 'WAITLISTED'
            ? 'Đội ' + r.teamName + ' sẽ rời danh sách chờ của giải.'
            : 'Đội ' + r.teamName + ' sẽ rời giải. Suất của đội được mời ngay cho đội đứng đầu danh sách chờ (nếu có) — đội đó nhận thông báo, có 24 giờ để xác nhận' +
              (fee > 0 ? ' và phải nộp lệ phí ' + money + ' mới được vào giải' : '') + '.';
        return head + (paid ? ' Lệ phí ' + money + ' đội đã nộp sẽ KHÔNG được hoàn lại.' : '') + ' Đơn đã rút không khôi phục được. Vẫn rút đơn?';
    }

    // Góp ý 28/11 (lần 2): nút "Đăng ký cho đội" KHÔNG biến mất (trước đây hiện rồi bị thay, nhìn như nháy) — đội đã có đơn
    // thì nút vẫn ở đó nhưng tối đi, không bấm được; bên cạnh chỉ thêm nút "Rút đơn" (có cảnh báo). Trạng thái đơn chi tiết
    // xem ở menu tài khoản → Đơn đăng ký của tôi. Đơn nháp vẫn để nút sáng (bấm để làm tiếp đơn).
    var LOGGED_HINT = (function () { try { return !!(window.Auth && (Auth.getAccessToken() || Auth.getUser())); } catch (e) { return false; } })();
    async function markMyRegistration(id) {
        var btn = document.getElementById('ov-register');
        if (!btn) return;
        var done = function () { btn.classList.remove('is-checking'); };
        var me = null; try { me = await Ui.currentUser(); } catch (e) { me = null; }
        if (!me) return done();
        var list;
        try { list = (await Api.get('/api/registrations/mine')).data || []; } catch (e) { return done(); }
        var mine = list.filter(function (r) { return Number(r.tournamentId) === Number(id) && ['APPROVED', 'PENDING', 'WAITLISTED'].indexOf(r.status) >= 0; })[0];
        done();
        if (!mine) return;
        btn.classList.add('is-registered');
        btn.removeAttribute('href');
        btn.setAttribute('aria-disabled', 'true');
        btn.setAttribute('tabindex', '-1');
        btn.title = 'Đội ' + mine.teamName + ' đã đăng ký giải này';
        var w = document.createElement('button');
        w.type = 'button';
        w.className = 'btn btn-ghost text-danger-etoms';
        w.textContent = 'Rút đơn';
        btn.insertAdjacentElement('afterend', w);
        w.addEventListener('click', async function () {
            if (!(await window.etomsConfirm(withdrawWarning(mine), { title: 'Rút đơn đăng ký', okText: 'Vẫn rút đơn' }))) return;
            w.disabled = true;
            try { var res = await Api.post('/api/registrations/' + mine.registrationId + '/withdraw', {}); await window.etomsAlert(res.message || 'Đã rút đơn.', { title: 'Đơn đăng ký' }); location.reload(); }
            catch (err) { w.disabled = false; await window.etomsAlert(err.message || 'Không thể rút đơn.', { title: 'Không thực hiện được' }); }
        });
    }

    // Danh sách chờ: ai xem trang giải cũng biết đang có bao nhiêu đội chờ suất và xem được thứ tự ở trang Đội tham dự.
    async function showWaitlist(id) {
        var holder = document.getElementById('ov-waitlist');
        if (!holder) return;
        var d; try { d = (await Api.get('/api/tournaments/' + id + '/participants')).data; } catch (e) { return; }
        if (!d) return;
        var n = (d.waitlisted || []).length, full = (d.approved || []).length >= d.maxTeams;
        if (!n && !full) return;
        holder.innerHTML = 'Danh sách chờ: <strong>' + n + ' đội</strong>' +
            (n ? ' (' + d.waitlisted.slice(0, 3).map(function (p) { return '#' + Number(p.waitlistPosition) + ' ' + esc(p.name); }).join(', ') + (n > 3 ? ', …' : '') + ')' : ' — đội nộp đơn từ bây giờ sẽ vào đây') +
            ' · <a href="/tournament/participants.html?id=' + Number(id) + '">Xem thứ tự</a>';
        holder.hidden = false;
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
            heroSub = (full ? 'Đã đủ ' + t.maxTeams + ' suất — đơn mới vào danh sách chờ.' : 'Còn ' + (t.maxTeams - t.approvedTeams) + ' trên ' + t.maxTeams + ' suất.') +
                (t.minTeams ? ' Cần ít nhất ' + t.minTeams + ' đội được duyệt để khai mạc.' : '');
            // Đã đăng nhập: nút hơi mờ trong lúc kiểm đội đã có đơn chưa (markMyRegistration), không nháy đổi sau đó.
            primary = '<a class="btn btn-primary' + (LOGGED_HINT ? ' is-checking' : '') + '" id="ov-register" data-hide-roles="ORGANIZER ADMIN" href="/registration/register.html?tournamentId=' + id + '">Đăng ký cho đội</a>';
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
        } else if (t.statusLabel === 'Chưa đủ đội') {
            // minTeams do máy chủ tính (TournamentLifecycle.MinTeams: 2 đội, Loại kép 3 đội).
            var need = t.minTeams || 2;
            heroText = 'Chưa đủ đội để khai mạc';
            heroSub = 'Mới có ' + Number(t.approvedTeams || 0) + ' đội được duyệt, cần ít nhất ' + need + ' đội. Ban tổ chức có thể gia hạn đăng ký hoặc hủy giải.';
            primary = '<a class="btn btn-primary" href="/tournament/participants.html?id=' + id + '">Xem đội tham dự</a>';
        } else if (at(t.startAt) !== null && at(t.startAt) <= now) {
            heroText = 'Đã đến giờ khai mạc — chờ Ban tổ chức bắt đầu giải';
            primary = '<a class="btn btn-primary" href="/tournament/participants.html?id=' + id + '">Xem đội tham dự</a>';
        } else {
            heroText = 'Đã đóng đăng ký — chờ khai mạc ' + fmtDateTime(t.startAt);
            primary = '<a class="btn btn-primary" href="/tournament/participants.html?id=' + id + '">Xem đội tham dự</a>';
        }
        var cta = '<section class="card-etoms ov-hero mb-3"><div class="body">' +
            '<div class="ov-hero-top"><div class="minw-0"><div class="ov-hero-title">' + esc(heroText) + '</div>' +
            (heroSub ? '<div class="hint">' + esc(heroSub) + '</div>' : '') +
            (t.registrationOpen ? '<div class="hint mt-1" id="ov-waitlist" hidden></div>' : '') + '</div>' +
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

    // Nhà tài trợ của giải (SPN, cùng nguồn với trang giới thiệu giải) — đặt CUỐI trang, dưới "Thông tin chi tiết",
    // tải sau phần chính. Giải chưa có nhà tài trợ thì không hiện khu này.
    var TIER = { DIAMOND: 'Kim cương', GOLD: 'Vàng', SILVER: 'Bạc', BRONZE: 'Đồng', PARTNER: 'Đối tác' };
    // UC-13 A4: tin Ban tổ chức đăng cho giải (5 tin mới nhất), bấm vào đọc ở trang Tin tức.
    async function loadNews(id) {
        var items;
        try { items = (((await Api.get('/api/tournaments/' + id + '/news')).data || {}).items || []).filter(function (n) { return n.status === 'PUBLISHED'; }); } catch (e) { return; }
        if (!items.length) return;
        var section = document.createElement('section');
        section.className = 'card-etoms mt-3';
        section.innerHTML = '<div class="body"><div class="ov-h">Tin tức của giải</div><div class="d-grid gap-2">' + items.slice(0, 5).map(function (n) {
            return '<a class="member-card" href="/news/index.html?id=' + Number(n.contentItemId) + '"><div class="grow min-w-0"><div class="nm text-truncate">' + esc(n.title) +
                '</div><div class="rl">' + esc(fmtDate(n.publishedAt)) + '</div></div></a>';
        }).join('') + '</div></div>';
        document.getElementById('overview-root').appendChild(section);
    }

    async function loadSponsors(id) {
        var list;
        try { list = ((await Api.get('/api/marketing/tournaments/' + id)).data || {}).sponsors || []; } catch (e) { return; }
        if (!list.length) return;
        var cards = list.map(function (s) {
            var site = Ui.safeUrl(s.websiteUrl), logo = Ui.safeUrl(s.logoUrl);
            // BR-SPN-01: cỡ logo theo hạng tài trợ (sponsor-tier-*).
            var tierClass = ' sponsor-tier-' + String(s.tier || 'PARTNER').toLowerCase();
            var inner = (logo ? '<img class="sponsor-logo' + tierClass + '" src="' + esc(logo) + '" alt="" loading="lazy">' : '') +
                '<span class="sponsor-name">' + esc(s.name) + '</span>' +
                '<span class="badge badge-gold">Hạng ' + esc(TIER[s.tier] || s.tier) + '</span>';
            return '<div class="col-6 col-md-4 col-lg-3">' + (site
                ? '<a class="ad-slot sponsor-card h-100" href="' + esc(site) + '" target="_blank" rel="noopener noreferrer sponsored">' + inner + '</a>'
                : '<div class="ad-slot sponsor-card h-100">' + inner + '</div>') + '</div>';
        }).join('');
        var section = document.createElement('section');
        section.className = 'card-etoms mt-3';
        section.innerHTML = '<div class="body"><div class="ov-h">Nhà tài trợ</div><div class="row g-3">' + cards + '</div></div>';
        document.getElementById('overview-root').appendChild(section);
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
            markMyRegistration(id);
            showWaitlist(id);
            await loadNews(id);
            loadSponsors(id);
        } catch (err) {
            // UC-36 E2: giải không tồn tại hoặc còn Nháp -> báo không tìm thấy, gợi ý về danh sách.
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>' +
                (err.status === 404 ? 'Không tìm thấy giải đấu' : 'Không tải được thông tin giải') + '</h3><p>' +
                Ui.escapeHtml(err.message || '') + '</p><a class="btn btn-primary mt-3" href="/tournament/index.html">Về danh sách giải</a></div></div>';
        }
    });
})();
