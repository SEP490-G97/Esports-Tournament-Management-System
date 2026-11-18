// Trang tổng hợp cho Admin: đếm việc chờ xử lý từ các API có sẵn và dẫn tới công cụ tương ứng.
// Không có nghiệp vụ mới — mọi thao tác vẫn làm ở trang đích.
// Các ô hiện ngay, số liệu ô nào về trước thì điền trước (không chờ API chậm nhất).
(function () {
    function count(value) {
        if (value && typeof value.total === 'number') return value.total; // danh sách có phân trang
        if (Array.isArray(value)) return value.length;
        if (value && Array.isArray(value.items)) return value.items.length;
        return null;
    }

    var TILES = [
        {
            key: 'orgreq', overline: 'Chờ cấp quyền', title: 'Đơn xin làm Ban tổ chức', primaryWhenPositive: true,
            text: 'Tuyển thủ xin làm Ban tổ chức (UC-43). Duyệt = cấp một tài khoản Ban tổ chức riêng, không có đội.',
            href: '#organizer-requests', action: 'Xem đơn',
            url: '/api/admin/organizer-requests', pick: count
        },
        {
            key: 'review', overline: 'Chờ xét duyệt', title: 'Hồ sơ đơn vị tổ chức', primaryWhenPositive: true,
            text: 'Duyệt hoặc từ chối hồ sơ đăng ký đơn vị (ORG-02). Từ chối phải ghi lý do.',
            href: '/organization/index.html#review-section', action: 'Mở hàng chờ duyệt',
            url: '/api/organizations/review-queue', pick: count
        },
        {
            key: 'accounts', overline: 'Tài khoản', title: 'Quản lý tài khoản',
            text: 'Tìm kiếm, lọc theo vai trò / trạng thái, khóa và mở khóa tài khoản (ADM-01/02).',
            href: '/admin/user.html', action: 'Quản lý tài khoản',
            url: '/api/admin/accounts?page=1&pageSize=1', pick: function (d) { return d && typeof d.total === 'number' ? d.total : null; }
        },
        {
            key: 'stats', overline: 'Thống kê', title: 'Thống kê hệ thống',
            text: 'Số tài khoản, đội, giải đấu, đơn đăng ký và trận đấu; biểu đồ theo tháng và theo tựa game.',
            href: '/admin/stats.html', action: 'Xem thống kê',
            url: '/api/admin/stats?months=3', pick: function (d) { return d && d.tiles ? d.tiles.tournaments : null; }
        },
        {
            key: 'ads', overline: 'Quảng cáo', title: 'Banner & thông báo hệ thống',
            text: 'Đăng banner sự kiện, quà tặng ở trang chủ và gửi thông báo tới mọi tài khoản theo vai trò.',
            href: '/admin/ads.html', action: 'Quản lý quảng cáo',
            url: '/api/admin/banners', pick: count
        },
        {
            key: 'news', overline: 'Tin tức', title: 'Tin tức ETOMS',
            text: 'Viết, ghim, ẩn tin của ETOMS hiện ở trang chủ và trang Tin tức.',
            href: '/admin/news.html', action: 'Quản lý tin tức',
            url: '/api/admin/news', pick: count
        },
        {
            key: 'rules', overline: 'Luật thi đấu', title: 'Bộ luật theo tựa game',
            text: 'Quản lý bộ luật, bản đồ và vị trí cho từng game (GRL-01..03).',
            href: '/admin/game-rules.html', action: 'Quản lý bộ luật',
            url: '/api/game-rules', pick: function (d) {
                var games = d && d.games ? d.games : [];
                return games.reduce(function (sum, g) { return sum + (g.rulesets ? g.rulesets.length : 0); }, 0);
            }
        },
        {
            key: 'orgs', overline: 'Đơn vị', title: 'Đơn vị tổ chức',
            text: 'Danh sách công khai các đơn vị tổ chức giải và điểm uy tín.',
            href: '/organization/index.html', action: 'Xem danh sách'
        }
        // Bỏ ô "Đội tuyển": Admin chỉ dùng khu quản trị, trang đội tuyển là trang của người chơi.
    ];

    function tile(t) {
        return '<div class="card-etoms admin-tile" data-tile="' + t.key + '">' +
            '<span class="profile-overline">' + Ui.escapeHtml(t.overline) + '</span>' +
            (t.url ? '<div class="num">…</div>' : '') +
            '<h2>' + Ui.escapeHtml(t.title) + '</h2>' +
            '<p>' + Ui.escapeHtml(t.text) + '</p>' +
            '<a class="btn btn-ghost btn-sm" href="' + t.href + '">' + Ui.escapeHtml(t.action) + '</a>' +
            '</div>';
    }

    async function fill(t) {
        var box = document.querySelector('[data-tile="' + t.key + '"]');
        var value = null;
        try {
            var res = await Api.get(t.url);
            value = t.pick(res.data !== undefined ? res.data : res);
        } catch (e) { value = null; }
        box.querySelector('.num').textContent = value === null || value === undefined ? '—' : String(value);
        if (t.primaryWhenPositive && value > 0) {
            var btn = box.querySelector('.btn');
            btn.classList.remove('btn-ghost');
            btn.classList.add('btn-primary');
        }
    }

    // ===== UC-43: đơn xin làm Ban tổ chức =====
    function fmtTime(iso) { return iso ? new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z').toLocaleString('vi-VN') : ''; }

    // Một đơn = đầu thẻ (người xin + lời nhắn) và HAI khối tách bạch: "Cấp tài khoản BTC" | "Từ chối".
    function requestCard(r) {
        var id = Number(r.accountId);
        var name = r.fullName || r.email || '?';
        return '<div class="card-etoms orq-card" data-req="' + id + '"><div class="body">' +
            '<div class="orq-head"><span class="orq-av" aria-hidden="true">' + Ui.escapeHtml(name.substring(0, 1).toUpperCase()) + '</span>' +
            '<div class="orq-who"><div class="orq-name">' + Ui.escapeHtml(name) + '</div>' +
            '<div class="orq-meta"><span>' + Ui.escapeHtml(r.email) + '</span><span>Gửi ' + Ui.escapeHtml(fmtTime(r.requestedAt)) + '</span>' +
            '<span>Đang ở ' + Number(r.activeTeams) + ' đội</span></div></div>' +
            '<span class="badge badge-gold">Chờ duyệt</span></div>' +
            '<div class="orq-label">Mong muốn của người xin</div>' +
            '<div class="orq-note">' + Ui.escapeHtml(r.note || 'Không ghi mong muốn.') + '</div>' +
            '<div class="orq-actions">' +
            '<div class="orq-box orq-approve"><div class="orq-box-title">Cấp tài khoản Ban tổ chức</div>' +
            '<p class="orq-box-hint">Tạo tài khoản BTC riêng (không có đội). Tài khoản tuyển thủ hiện tại giữ nguyên.</p>' +
            '<div class="field"><label for="orq-email-' + id + '">Email đăng nhập BTC <span class="req">*</span></label>' +
            '<input id="orq-email-' + id + '" class="input" type="email" maxlength="254" value="' + Ui.escapeHtml(r.proposedEmail || '') + '" placeholder="Email riêng, khác email tuyển thủ" />' +
            '<span class="field-error" data-err="OrganizerEmail"></span></div>' +
            '<div class="field"><label for="orq-name-' + id + '">Tên hiển thị BTC</label>' +
            '<input id="orq-name-' + id + '" class="input" maxlength="100" placeholder="' + Ui.escapeHtml(name + ' (BTC)') + '" />' +
            '<span class="field-error" data-err="OrganizerFullName"></span></div>' +
            '<button type="button" class="btn btn-primary w-100" data-approve="' + id + '">Cấp tài khoản BTC</button></div>' +
            '<div class="orq-box orq-reject"><div class="orq-box-title">Từ chối đơn</div>' +
            '<p class="orq-box-hint">Người xin nhận thông báo kèm lý do.</p>' +
            '<div class="field"><label for="orq-reason-' + id + '">Lý do từ chối <span class="req">*</span></label>' +
            '<textarea id="orq-reason-' + id + '" class="input" rows="4" maxlength="500" placeholder="5–500 ký tự"></textarea>' +
            '<span class="field-error" data-err="Reason"></span></div>' +
            '<button type="button" class="btn btn-ghost w-100" data-reject="' + id + '">Từ chối</button></div>' +
            '</div><div class="orq-result mt-2" aria-live="polite"></div></div></div>';
    }

    async function loadRequests() {
        var root = document.getElementById('organizer-requests-list');
        try {
            var list = await Api.get('/api/admin/organizer-requests');
            list = Array.isArray(list) ? list : (list.data || []);
            root.innerHTML = list.length ? list.map(requestCard).join('')
                : '<div class="card-etoms"><div class="empty"><p>Không có đơn nào đang chờ.</p></div></div>';
            wireRequests();
        } catch (err) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    }

    function showErrors(card, fieldErrors) {
        card.querySelectorAll('[data-err]').forEach(function (s) { s.textContent = ''; });
        Object.keys(fieldErrors || {}).forEach(function (k) {
            var s = card.querySelector('[data-err="' + k + '"]');
            var v = fieldErrors[k];
            if (s) s.textContent = Array.isArray(v) ? v[0] : v;
        });
    }

    function wireRequests() {
        document.querySelectorAll('[data-approve]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                var id = btn.dataset.approve, card = btn.closest('.orq-card');
                var email = document.getElementById('orq-email-' + id).value.trim();
                var name = document.getElementById('orq-name-' + id).value.trim();
                showErrors(card, {});
                if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) { showErrors(card, { OrganizerEmail: 'Nhập email hợp lệ cho tài khoản Ban tổ chức.' }); return; }
                if (window.etomsConfirm && !(await window.etomsConfirm('Cấp tài khoản Ban tổ chức ' + email + '?'))) return;
                btn.disabled = true;
                try {
                    var res = await Api.post('/api/admin/organizer-requests/' + id + '/approve', { OrganizerEmail: email, OrganizerFullName: name || null });
                    var d = res.data;
                    // Mật khẩu tạm chỉ hiện MỘT lần để Admin bàn giao (đồng thời đã gửi email nếu máy chủ có SMTP).
                    card.querySelector('.orq-actions').remove();
                    card.querySelector('.orq-result').innerHTML = '<div class="alert alert-success mb-0">Đã cấp tài khoản <strong>' + Ui.escapeHtml(d.organizerEmail) +
                        '</strong>. Mật khẩu tạm: <code class="orq-pass">' + Ui.escapeHtml(d.temporaryPassword) + '</code> ' +
                        '<button type="button" class="btn btn-ghost btn-sm" data-copy>Sao chép</button><br/>' +
                        (d.emailSent ? 'Đã gửi email cho tài khoản mới.' : 'Máy chủ chưa gửi được email — hãy bàn giao mật khẩu này trực tiếp.') +
                        ' Mật khẩu chỉ hiện một lần.</div>';
                    var copy = card.querySelector('[data-copy]');
                    copy.addEventListener('click', function () { navigator.clipboard && navigator.clipboard.writeText(d.temporaryPassword); copy.textContent = 'Đã chép'; });
                } catch (err) {
                    showErrors(card, err.fieldErrors);
                    Ui.showAlert(card.querySelector('.orq-result'), 'danger', err.message);
                    btn.disabled = false;
                }
            });
        });
        document.querySelectorAll('[data-reject]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                var id = btn.dataset.reject, card = btn.closest('.orq-card');
                var reason = document.getElementById('orq-reason-' + id).value.trim();
                showErrors(card, {});
                if (reason.length < 5 || reason.length > 500) { showErrors(card, { Reason: 'Từ chối phải ghi lý do (5–500 ký tự).' }); return; }
                btn.disabled = true;
                try {
                    var res = await Api.post('/api/admin/organizer-requests/' + id + '/reject', { Reason: reason });
                    card.querySelector('.orq-actions').remove();
                    card.querySelector('.orq-result').innerHTML = '<div class="alert alert-secondary mb-0">' + Ui.escapeHtml(res.message || 'Đã từ chối.') + '</div>';
                } catch (err) {
                    showErrors(card, err.fieldErrors);
                    Ui.showAlert(card.querySelector('.orq-result'), 'danger', err.message);
                    btn.disabled = false;
                }
            });
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ADMIN']);
        if (!me) return;

        document.getElementById('admin-root').innerHTML = '<div class="admin-grid">' + TILES.map(tile).join('') + '</div>' +
            '<section id="organizer-requests" class="mt-4" aria-labelledby="orq-title">' +
            '<div class="section-title" id="orq-title">Đơn xin làm Ban tổ chức</div>' +
            '<div class="section-sub">Duyệt đơn = tạo một tài khoản Ban tổ chức RIÊNG (email khác, không có đội). Tài khoản tuyển thủ của người xin giữ nguyên đội.</div>' +
            '<div id="organizer-requests-list" class="d-grid gap-3"><div class="hint">Đang tải…</div></div></section>';
        TILES.filter(function (t) { return t.url; }).forEach(fill);
        await loadRequests();
        if (location.hash === '#organizer-requests') document.getElementById('organizer-requests').scrollIntoView({ block: 'start' });
    });
})();
