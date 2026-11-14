// "Layout" dùng chung cho mọi trang tĩnh: chèn nav.html/confirm-modal.html, bật theme-toggle,
// Mọi trang phải có sẵn <div id="site-header"></div> và <div id="confirm-modal-root"></div>,
// và nạp theo thứ tự: auth.js, api.js, ui.js, layout.js (theme-init.js nạp riêng trong <head>).
(function () {
    function applyActiveNav() {
        var path = location.pathname.toLowerCase();
        document.querySelectorAll('#etomsNavCollapse nav a[data-nav]').forEach(function (a) {
            var key = a.getAttribute('data-nav');
            var match = (key === 'home' && path === '/' ) ||
                (key !== 'home' && path.indexOf('/' + key) === 0);
            a.classList.toggle('active', match);
        });
    }

    function wireThemeToggle() {
        var btn = document.getElementById('theme-toggle');
        if (!btn) return;
        btn.addEventListener('click', function () {
            var isLight = document.documentElement.getAttribute('data-theme') === 'light';
            if (isLight) {
                document.documentElement.removeAttribute('data-theme');
                try { localStorage.setItem('etoms-theme', 'dark'); } catch (e) {}
            } else {
                document.documentElement.setAttribute('data-theme', 'light');
                try { localStorage.setItem('etoms-theme', 'light'); } catch (e) {}
            }
        });
    }

    // Người dùng hiện tại (JWT, auth.js) — dùng chung kết quả với trang qua Ui.currentUser().
    async function resolveCurrentUser() {
        var me = await Ui.currentUser();
        return me || { isAuthenticated: false };
    }

    async function wireAuthWidget() {
        var slot = document.getElementById('nav-auth-slot');
        if (!slot) return;
        var loggedOutHtml = '<a class="btn btn-primary nav-auth-login" href="/auth/login.html">Đăng nhập</a>';
        try {
            var me = await resolveCurrentUser();
            if (me.isAuthenticated) {
                renderUserDropdown(slot, me);
                await loadMyTeam(me);
            } else {
                slot.innerHTML = loggedOutHtml;
            }
            applyRoleVisibility(me);
            return me;
        } catch (e) {
            slot.innerHTML = loggedOutHtml;
            applyRoleVisibility({ isAuthenticated: false });
            return { isAuthenticated: false };
        }
    }

    // Khung "Xin chào, X" giờ là 1 dropdown: bấm avatar/tên để mở, bên trong có "Đội của tôi"
    // (nối thẳng tới team/details.html?id=, hoặc báo "Không có đội" nếu chưa ở đội nào) + Đăng xuất.
    function renderUserDropdown(slot, me) {
        slot.innerHTML =
            '<div class="nav-user-dd" id="nav-user-dd">' +
            '  <button type="button" class="nav-user-trigger" id="nav-user-trigger" aria-expanded="false">' +
            '    <span class="av">' + Ui.escapeHtml((me.displayName || '?').substring(0, 1).toUpperCase()) + '</span>' +
            '    <span><span class="nav-hello">Xin chào, </span><span class="who">' + Ui.escapeHtml(me.displayName || '') + '</span></span>' +
            '    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" class="nav-user-caret"><path d="M6 9l6 6 6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>' +
            '  </button>' +
            '  <div class="nav-user-menu" id="nav-user-menu">' +
            (me.role ? '    <span class="nav-user-role">' + Ui.escapeHtml(roleLabel(me.role)) + '</span>' : '') +
            // Theo PermissionMatrix: đội (TEAM) và đơn đăng ký (REG-01..05) chỉ của tuyển thủ/đội trưởng;
            // tạo giải (TOUR) và truyền thông (MKT) chỉ của Ban tổ chức.
            (me.role === 'PLAYER'
                ? '    <a class="nav-user-menu-item nav-user-myteam" id="nav-user-myteam" href="/team/mine.html">Các đội của tôi</a>' : '') +
            '    <a class="nav-user-menu-item" href="/profile/index.html">Hồ sơ cá nhân</a>' +
            (me.role === 'PLAYER'
                ? '    <a class="nav-user-menu-item" href="/registration/mine.html">Đơn đăng ký của tôi</a>' : '') +
            (me.role === 'ORGANIZER'
                ? '    <a class="nav-user-menu-item" href="/registration/organizer.html">Giải đấu của tôi</a>' +
                  '    <a class="nav-user-menu-item" href="/tournament-setup/index.html">Tạo giải mới</a>' +
                  '    <a class="nav-user-menu-item" href="/marketing/tools.html">Công cụ truyền thông</a>' : '') +
            (me.role === 'ADMIN' ? '    <a class="nav-user-menu-item" href="/admin/index.html">Bảng quản trị</a>' : '') +
            '    <div class="nav-user-menu-sep"></div>' +
            '    <a class="nav-user-menu-item" href="#" id="nav-logout">Đăng xuất</a>' +
            '  </div>' +
            '</div>';

        var trigger = document.getElementById('nav-user-trigger');
        var menu = document.getElementById('nav-user-menu');
        trigger.addEventListener('click', function (e) {
            e.stopPropagation();
            var open = menu.classList.toggle('show');
            trigger.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
        document.addEventListener('click', function (e) {
            if (!menu.classList.contains('show')) return;
            if (!menu.contains(e.target) && e.target !== trigger) {
                menu.classList.remove('show');
                trigger.setAttribute('aria-expanded', 'false');
            }
        });
        document.getElementById('nav-logout').addEventListener('click', function (e) {
            e.preventDefault();
            Auth.logout('/');                                  // thu hồi refresh token, xóa cookie, về trang chủ
        });
    }

    async function loadMyTeam(me) {
        var box = document.getElementById('nav-user-myteam');
        if (!box) return;
        // Một tài khoản có thể ở nhiều đội (mỗi tựa game một đội) nên chỉ hiện SỐ đội;
        // bấm vào mở trang /team/mine.html liệt kê đủ. Lỗi thì giữ nguyên link, không có số.
        try {
            // Api.get tự gửi kèm JWT khi đã đăng nhập thật, và dùng chung kết quả với trang nếu trang cũng hỏi.
            var teams = await Api.get('/api/teams/my-teams');
            var count = Array.isArray(teams) ? teams.length : 0;
            box.innerHTML = 'Các đội của tôi <span class="badge badge-soft">' + count + '</span>';
        } catch (e) { /* giữ link không có số */ }
    }

    function roleLabel(role) {
        return { ADMIN: 'Quản trị viên', ORGANIZER: 'Ban tổ chức', PLAYER: 'Tuyển thủ' }[role] || role;
    }

    // Hiện mục menu theo vai trò (data-roles trong nav.html): "auth" = đã đăng nhập, còn lại là danh
    // sách Role (vd "ORGANIZER,ADMIN" cho hàng đợi tranh chấp RES-06). Mặc định các mục này ẩn bằng CSS
    // để không hiện rồi mất khi /me chạy xong. Chỉ là gợi ý hiển thị: quyền thật vẫn chặn ở API/trang đích.
    function applyRoleVisibility(me) {
        document.documentElement.setAttribute('data-role', me.isAuthenticated ? (me.role || 'PLAYER') : 'GUEST');
        var extra = 0;
        document.querySelectorAll('#etomsNavCollapse nav a[data-roles]').forEach(function (a) {
            var roles = a.getAttribute('data-roles').split(',');
            var ok = !!me.isAuthenticated && (roles.indexOf('auth') >= 0 || roles.indexOf(me.role) >= 0);
            a.classList.toggle('role-visible', ok);
            if (ok) extra++;
        });
        // Ban tổ chức/Admin có thêm mục menu: nới khung header để menu nằm trên một dòng.
        var header = document.querySelector('.etoms-nav');
        if (header) header.classList.toggle('nav-many', extra >= 2);
    }

    // Footer dùng chung cho mọi trang chưa có footer riêng (trang chủ giữ footer của nó).
    async function injectFooter() {
        if (document.querySelector('.site-footer')) return;
        var holder = document.createElement('div');
        holder.id = 'site-footer';
        var main = document.querySelector('main');
        if (main && main.parentNode) main.parentNode.insertBefore(holder, main.nextSibling);
        else document.body.appendChild(holder);
        await injectPartial('/partials/footer.html', 'site-footer');
    }

    // Thanh tab trong một giải đấu (giống các nền tảng esports): tab công khai cho mọi người,
    // tab quản lý chỉ hiện khi tài khoản là ban tổ chức của giải (API publish-status trả 200).
    var TOUR_PUBLIC = [
        ['/bracket/view.html', 'Tổng quan'],
        ['/tournament/bracket.html', 'Nhánh đấu'],
        ['/tournament/standings.html', 'Bảng xếp hạng'],
        ['/tournament/rulebook-history.html', 'Thể lệ']
    ];
    // Menu của Ban tổ chức chia nhóm (hiện thành cột bên trái trên máy tính).
    var TOUR_ORGANIZER_GROUPS = [
        ['Điều hành giải', [
            ['/tournament/manage.html', 'Trạng thái & xuất bản'],
            ['/tournament/dashboard.html', 'Bảng điều hành'],
            ['/registration/review.html', 'Đơn đăng ký', 'tournamentId'],
            ['/bracket/manage.html', 'Chia nhánh'],
            ['/tournament/schedule.html', 'Lịch thi đấu'],
            ['/tournament/referees.html', 'Trọng tài'],
            ['/tournament/stage-transition.html', 'Chuyển vòng']
        ]],
        ['Cấu hình giải', [
            ['/tournament/edit.html', 'Sửa thông tin'],
            ['/tournament/rulebook.html', 'Soạn thể lệ'],
            ['/tournament/ruleset.html', 'Bộ luật'],
            ['/tournament/clone.html', 'Nhân bản'],
            ['/tournament/cancel.html', 'Hủy giải']
        ]]
    ];

    function tournamentIdOfPage() {
        var path = location.pathname.toLowerCase();
        var q = new URLSearchParams(location.search);
        if (path === '/registration/review.html') return q.get('tournamentId');
        if (path.indexOf('/tournament/') === 0 || path === '/bracket/manage.html' || path === '/bracket/view.html' || path === '/bracket/tiebreaker.html') return q.get('id');
        return null;
    }

    async function wireTournamentNav(me) {
        var id = parseInt(tournamentIdOfPage(), 10);
        var main = document.querySelector('main');
        if (!id || !main) return;
        var isOrganizer = false;
        if (me && me.isAuthenticated && (me.role === 'ORGANIZER' || me.role === 'ADMIN')) {
            try { await Api.get('/api/tournaments/' + id + '/publish-status'); isOrganizer = true; } catch (e) { isOrganizer = false; }
        }
        var path = location.pathname.toLowerCase();
        function link(item) {
            var href = item[0] + '?' + (item[2] || 'id') + '=' + id;
            return '<a href="' + href + '"' + (path === item[0] ? ' class="active" aria-current="page"' : '') + '>' + Ui.escapeHtml(item[1]) + '</a>';
        }

        // Phần đầu trang (link quay lại + tiêu đề) giữ nguyên ở trên; menu chèn ngay sau.
        var head = main.querySelector(':scope > .page-head') ||
            Array.prototype.find.call(main.children, function (el) { return el.querySelector && el.querySelector('.page-head'); });

        if (!isOrganizer) {
            var bar = document.createElement('nav');
            bar.className = 'tour-subnav';
            bar.setAttribute('aria-label', 'Điều hướng trong giải');
            bar.innerHTML = TOUR_PUBLIC.map(link).join('');
            if (head) head.parentNode.insertBefore(bar, head.nextSibling);
            else main.insertBefore(bar, main.firstChild);
            return;
        }

        // Ban tổ chức: cột menu bên trái chia nhóm + nội dung bên phải.
        var aside = document.createElement('nav');
        aside.className = 'tour-sidenav';
        aside.setAttribute('aria-label', 'Điều hướng trong giải');
        aside.innerHTML = '<div class="grp"><div class="grp-title">Xem công khai</div>' + TOUR_PUBLIC.map(link).join('') + '</div>' +
            TOUR_ORGANIZER_GROUPS.map(function (g) {
                return '<div class="grp"><div class="grp-title">' + Ui.escapeHtml(g[0]) + '</div>' + g[1].map(link).join('') + '</div>';
            }).join('');
        var content = document.createElement('div');
        content.className = 'tour-content';
        var shell = document.createElement('div');
        shell.className = 'tour-shell';
        shell.appendChild(aside);
        shell.appendChild(content);

        // Chuyển mọi phần tử SAU tiêu đề vào cột nội dung (giữ nguyên phần tử, id, sự kiện đã gắn).
        var start = head ? head.nextSibling : main.firstChild;
        while (start && start.classList && start.classList.contains('back-link')) start = start.nextSibling;
        var moving = [];
        for (var node = start; node; node = node.nextSibling) moving.push(node);
        moving.forEach(function (n) { content.appendChild(n); });
        main.appendChild(shell);

        var active = aside.querySelector('a.active');
        if (active && active.scrollIntoView && window.matchMedia('(max-width: 991.98px)').matches)
            active.scrollIntoView({ block: 'nearest', inline: 'center' });
    }

    // Nút "← Quay lại" cho mọi trang chưa có sẵn link quay lại: đi từ trang khác trong web thì về trang
    // trước (giữ bộ lọc/vị trí cuộn), mở thẳng link thì về trang chủ.
    var NO_BACK = ['/', '/index.html', '/auth/login.html', '/auth/register.html'];
    function injectBackLink() {
        var main = document.querySelector('main');
        if (!main || NO_BACK.indexOf(location.pathname.toLowerCase()) >= 0 || main.querySelector('.back-link')) return;
        var link = document.createElement('a');
        link.className = 'back-link back-link-auto';
        link.href = '/';
        link.textContent = '← Quay lại';
        link.addEventListener('click', function (e) {
            var fromHere = false;
            try { fromHere = !!document.referrer && new URL(document.referrer).origin === location.origin; } catch (err) { }
            if (fromHere && history.length > 1) { e.preventDefault(); history.back(); }
        });
        main.insertBefore(link, main.firstChild);
    }

    // "Đang tải..." trong khung rỗng có vòng xoay nhỏ (trang JS thay nội dung khi tải xong).
    function markLoadingStates() {
        document.querySelectorAll('.empty h3').forEach(function (h) {
            if (/^Đang tải/.test((h.textContent || '').trim())) h.parentElement.classList.add('is-loading');
        });
    }

    // Hộp thoại của hệ thống thay cho confirm()/alert()/prompt() mặc định của trình duyệt:
    //   await etomsConfirm('...', { title, okText })            -> true/false
    //   await etomsAlert('...', { title })                       -> (đóng)
    //   await etomsPrompt('...', { title, placeholder, defaultValue, required, minLength, maxLength,
    //                             pattern, patternMessage, okText }) -> chuỗi đã trim, hoặc null nếu Hủy
    var pending = null;   // { resolve, kind, opts }
    function wireConfirmModal() {
        var overlay = document.getElementById('etoms-modal');
        if (!overlay) return;
        var title = document.getElementById('etoms-modal-title');
        var msg = document.getElementById('etoms-modal-msg');
        var field = document.getElementById('etoms-modal-field');
        var input = document.getElementById('etoms-modal-input');
        var error = document.getElementById('etoms-modal-error');
        var okBtn = document.getElementById('etoms-modal-ok');
        var cancelBtn = document.getElementById('etoms-modal-cancel');

        function finish(result) {
            overlay.classList.remove('show');
            if (pending) { var p = pending; pending = null; p.resolve(result); }
        }
        function cancel() {
            if (!pending) return;
            finish(pending.kind === 'confirm' ? false : pending.kind === 'prompt' ? null : undefined);
        }
        function validate(value, o) {
            if (o.required !== false && !value) return 'Vui lòng nhập nội dung.';
            if (value && o.minLength && value.length < o.minLength) return 'Cần ít nhất ' + o.minLength + ' ký tự.';
            if (value && value.length > (o.maxLength || 500)) return 'Tối đa ' + (o.maxLength || 500) + ' ký tự.';
            if (value && o.pattern && !o.pattern.test(value)) return o.patternMessage || 'Giá trị không hợp lệ.';
            return '';
        }
        okBtn.addEventListener('click', function () {
            if (!pending) return;
            if (pending.kind !== 'prompt') { finish(pending.kind === 'confirm' ? true : undefined); return; }
            var value = input.value.trim();
            var err = validate(value, pending.opts);
            error.textContent = err;
            if (err) { input.focus(); return; }
            finish(value);
        });
        cancelBtn.addEventListener('click', cancel);
        overlay.addEventListener('click', function (e) { if (e.target === overlay) cancel(); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && overlay.classList.contains('show')) cancel(); });
        input.addEventListener('input', function () { error.textContent = ''; });

        function open(kind, message, opts) {
            opts = opts || {};
            if (pending) cancel();
            return new Promise(function (resolve) {
                pending = { resolve: resolve, kind: kind, opts: opts };
                title.textContent = opts.title || (kind === 'alert' ? 'Thông báo' : kind === 'prompt' ? 'Nhập thông tin' : 'Xác nhận');
                msg.textContent = message || '';
                field.hidden = kind !== 'prompt';
                cancelBtn.hidden = kind === 'alert';
                okBtn.textContent = opts.okText || (kind === 'alert' ? 'Đóng' : 'Đồng ý');
                error.textContent = '';
                if (kind === 'prompt') {
                    input.value = opts.defaultValue || '';
                    input.placeholder = opts.placeholder || '';
                    input.maxLength = opts.maxLength || 500;
                    input.rows = opts.multiline === false ? 1 : 3;
                }
                overlay.classList.add('show');
                (kind === 'prompt' ? input : okBtn).focus();
            });
        }

        // Dùng cho hành động điều khiển bằng JS (nút gọi fetch): await etomsConfirm('...')
        window.etomsConfirm = function (message, opts) { return open('confirm', message, opts); };
        window.etomsAlert = function (message, opts) { return open('alert', message, opts); };
        window.etomsPrompt = function (message, opts) { return open('prompt', message, opts); };

        // Link tệp riêng tư: <a href="/api/.../file" data-private-file="ten-tep.jpg"> — tải kèm token.
        document.addEventListener('click', async function (e) {
            var link = e.target.closest && e.target.closest('a[data-private-file]');
            if (!link) return;
            e.preventDefault();
            try {
                await Api.openFile(link.getAttribute('href'), link.dataset.privateFile);
            } catch (err) {
                window.etomsAlert(err.message || 'Không mở được tệp.', { title: 'Không mở được tệp' });
            }
        });

        // Vẫn hỗ trợ <form data-confirm="..."> submit thuần (không qua fetch), y hệt bản cũ.
        document.addEventListener('submit', function (e) {
            var form = e.target;
            if (form.dataset && form.dataset.confirm && !form.dataset.confirmed) {
                e.preventDefault();
                window.etomsConfirm(form.dataset.confirm).then(function (ok) {
                    if (ok) { form.dataset.confirmed = '1'; form.submit(); }
                });
            }
        });
    }

    async function injectPartial(url, targetId) {
        var target = document.getElementById(targetId);
        if (!target) return;
        try {
            var res = await fetch(url);
            target.innerHTML = await res.text();
        } catch (e) { /* thiếu nav/modal vẫn chạy tiếp được, không chặn trang */ }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        // Trang nào quên chỗ chứa hộp thoại thì tự tạo, để mọi trang đều dùng được etomsConfirm/Prompt/Alert.
        if (!document.getElementById('confirm-modal-root')) {
            var modalRoot = document.createElement('div');
            modalRoot.id = 'confirm-modal-root';
            document.body.appendChild(modalRoot);
        }
        await Promise.all([
            injectPartial('/partials/nav.html', 'site-header'),
            injectPartial('/partials/confirm-modal.html', 'confirm-modal-root')
        ]);
        applyActiveNav();
        injectBackLink();
        wireThemeToggle();
        wireConfirmModal();
        injectFooter();
        var mePromise = wireAuthWidget();
        document.dispatchEvent(new CustomEvent('etoms:layout-ready'));
        wireTournamentNav(await mePromise);
    });

    // layout.js nạp ở cuối <body>, trước script trang (defer): lúc này khung "Đang tải..." còn nguyên.
    markLoadingStates();
})();
