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

    // Góp ý 23/11: các ô lối tắt trùng hệ menu quản trị phía trên và làm trang dài, tràn trên màn hình nhỏ.
    // Chỉ giữ 2 việc CẦN Admin xử lý ở dạng thẻ; phần còn lại thu về một hàng số liệu nhỏ.
    var TASKS = [
        {
            key: 'orgreq', title: 'Đơn xin làm Ban tổ chức', href: '#organizer-requests', action: 'Xem đơn',
            text: 'Duyệt = cấp một tài khoản Ban tổ chức riêng, không có đội.',
            url: '/api/admin/organizer-requests', pick: count
        },
        {
            key: 'review', title: 'Hồ sơ đơn vị chờ duyệt', href: '/organization/index.html#review-section', action: 'Mở hàng chờ duyệt',
            text: 'Duyệt hoặc từ chối hồ sơ đăng ký đơn vị; từ chối phải ghi lý do.',
            url: '/api/organizations/review-queue', pick: count
        }
    ];
    var STATS = [
        { key: 'accounts', label: 'Tài khoản', href: '/admin/user.html', url: '/api/admin/accounts?page=1&pageSize=1', pick: function (d) { return d && typeof d.total === 'number' ? d.total : null; } },
        { key: 'stats', label: 'Giải đấu', href: '/admin/stats.html', url: '/api/admin/stats?months=3', pick: function (d) { return d && d.tiles ? d.tiles.tournaments : null; } },
        { key: 'news', label: 'Tin tức', href: '/admin/news.html', url: '/api/admin/news', pick: count },
        { key: 'ads', label: 'Banner', href: '/admin/ads.html', url: '/api/admin/banners', pick: count },
        { key: 'games', label: 'Tựa game', href: '/admin/games.html', url: '/api/admin/games', pick: count },
        {
            key: 'rules', label: 'Bộ luật', href: '/admin/game-rules.html', url: '/api/game-rules', pick: function (d) {
                var games = d && d.games ? d.games : [];
                return games.reduce(function (sum, g) { return sum + (g.rulesets ? g.rulesets.length : 0); }, 0);
            }
        }
    ];

    function taskCard(t) {
        return '<div class="card-etoms admin-task" data-tile="' + t.key + '">' +
            '<div class="num">…</div><div class="admin-task-body"><h2>' + Ui.escapeHtml(t.title) + '</h2>' +
            '<p>' + Ui.escapeHtml(t.text) + '</p></div>' +
            '<a class="btn btn-ghost btn-sm" href="' + t.href + '">' + Ui.escapeHtml(t.action) + '</a></div>';
    }

    function statChip(s) {
        return '<a class="admin-stat" data-tile="' + s.key + '" href="' + s.href + '"><span class="num">…</span><span>' + Ui.escapeHtml(s.label) + '</span></a>';
    }

    async function fill(t) {
        var box = document.querySelector('[data-tile="' + t.key + '"]');
        var value = null;
        try {
            var res = await Api.get(t.url);
            value = t.pick(res.data !== undefined ? res.data : res);
        } catch (e) { value = null; }
        box.querySelector('.num').textContent = value === null || value === undefined ? '—' : String(value);
        // Việc đang chờ thì làm nổi thẻ + nút để Admin thấy ngay.
        if (box.classList.contains('admin-task') && value > 0) {
            box.classList.add('has-work');
            var btn = box.querySelector('.btn');
            btn.classList.remove('btn-ghost');
            btn.classList.add('btn-primary');
        }
    }

    // ===== UC-43: đơn xin làm Ban tổ chức =====
    function fmtTime(iso) { return Ui.fmtDateTime(iso); }

    // Một đơn = đầu thẻ (người xin + lời nhắn) và HAI khối tách bạch: "Cấp tài khoản BTC" | "Từ chối".
    function requestCard(r) {
        var id = Number(r.accountId);
        var name = r.fullName || r.email || '?';
        return '<div class="card-etoms orq-card" data-req="' + id + '"><div class="body">' +
            '<div class="orq-head"><span class="orq-av" aria-hidden="true">' + Ui.avatarInner(r.avatarUrl, name) + '</span>' +
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

    // Yêu cầu xem lại mật khẩu Ban tổ chức (góp ý 28/11). Không có yêu cầu thì ẩn cả mục.
    async function loadReissues() {
        var section = document.getElementById('organizer-reissues'), root = document.getElementById('organizer-reissues-list');
        var list;
        try { list = await Api.get('/api/admin/organizer-requests/reissues'); list = Array.isArray(list) ? list : (list.data || []); } catch (e) { return; }
        section.hidden = !list.length;
        root.innerHTML = list.map(function (r) {
            return '<div class="card-etoms"><div class="body d-flex align-items-center gap-3 flex-wrap">' +
                '<span class="orq-av" aria-hidden="true">' + Ui.avatarInner(r.avatarUrl, r.fullName) + '</span>' +
                '<div class="flex-grow-1 min-w-0"><div class="fw-bold">' + Ui.escapeHtml(r.fullName) + '</div>' +
                '<div class="hint">' + Ui.escapeHtml(r.email) + ' · tài khoản Ban tổ chức ' + Ui.escapeHtml(r.organizerEmail || '—') + ' · xin lúc ' + Ui.escapeHtml(fmtTime(r.requestedAt)) + '</div></div>' +
                '<button type="button" class="btn btn-primary btn-sm" data-reissue="' + Number(r.accountId) + '">Cho xem lại mật khẩu</button>' +
                '<span class="orr-result"></span></div></div>';
        }).join('');
        root.querySelectorAll('[data-reissue]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                if (!(await window.etomsConfirm('Cho người này xem mật khẩu mới thêm một lần? Mật khẩu cũ của tài khoản Ban tổ chức sẽ hết hiệu lực khi họ xem.', { title: 'Xác nhận xem lại mật khẩu', okText: 'Xác nhận' }))) return;
                btn.disabled = true;
                try {
                    var res = await Api.post('/api/admin/organizer-requests/' + btn.dataset.reissue + '/reissue-approve', {});
                    btn.parentElement.querySelector('.orr-result').textContent = res.message || 'Đã xác nhận.';
                    btn.remove();
                } catch (err) { btn.disabled = false; btn.parentElement.querySelector('.orr-result').textContent = err.message; }
            });
        });
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
                    // Góp ý 28/11: Admin không thấy mật khẩu — người xin xem mật khẩu đúng một lần ở hồ sơ của họ.
                    card.querySelector('.orq-actions').remove();
                    card.querySelector('.orq-result').innerHTML = '<div class="alert alert-success mb-0">Đã cấp tài khoản <strong>' + Ui.escapeHtml(d.organizerEmail) +
                        '</strong>. Người xin đã nhận thông báo và xem mật khẩu <strong>một lần</strong> ở Hồ sơ → Xin làm Ban tổ chức. ' +
                        'Muốn xem lại, họ gửi yêu cầu và bạn xác nhận ở mục "Yêu cầu xem lại mật khẩu" bên dưới.</div>';
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

        document.getElementById('admin-root').innerHTML =
            '<div class="section-title mt-0">Việc cần xử lý</div><div class="admin-tasks">' + TASKS.map(taskCard).join('') + '</div>' +
            '<nav class="admin-stats" aria-label="Số liệu nhanh">' + STATS.map(statChip).join('') + '</nav>' +
            '<section id="organizer-requests" class="mt-4" aria-labelledby="orq-title">' +
            '<div class="section-title" id="orq-title">Đơn xin làm Ban tổ chức</div>' +
            '<div class="section-sub">Duyệt đơn = tạo một tài khoản Ban tổ chức RIÊNG (email khác, không có đội). Tài khoản tuyển thủ của người xin giữ nguyên đội.</div>' +
            '<div id="organizer-requests-list" class="d-grid gap-3"><div class="hint">Đang tải…</div></div></section>' +
            '<section id="organizer-reissues" class="mt-4" aria-labelledby="orr-title" hidden>' +
            '<div class="section-title" id="orr-title">Yêu cầu xem lại mật khẩu Ban tổ chức</div>' +
            '<div class="section-sub">Người xin đã xem mật khẩu một lần và xin xem lại. Xác nhận thì họ xem được mật khẩu MỚI thêm một lần (mật khẩu cũ hết hiệu lực).</div>' +
            '<div id="organizer-reissues-list" class="d-grid gap-3"></div></section>';
        TASKS.concat(STATS).forEach(fill);
        loadReissues();
        await loadRequests();
        if (location.hash === '#organizer-reissues') { var rs = document.getElementById('organizer-reissues'); if (rs) rs.scrollIntoView({ block: 'start' }); }
        if (location.hash === '#organizer-requests') document.getElementById('organizer-requests').scrollIntoView({ block: 'start' });
    });
})();
