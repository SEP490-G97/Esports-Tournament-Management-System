// Các đội của tôi: mọi đội tài khoản đang là thành viên (mỗi tựa game tối đa một đội).
// Đội đang làm đội trưởng có một nút vào Bảng điều khiển; danh sách phân trang 6 đội/trang.
(function () {
    function logo(t) {
        return t.logoUrl
            ? '<img class="team-logo" src="' + Ui.escapeHtml(t.logoUrl) + '" alt="Logo ' + Ui.escapeHtml(t.name) + '" />'
            : '<span class="team-logo">' + Ui.escapeHtml(t.tag ? t.tag.substring(0, 1) : '?') + '</span>';
    }

    function card(t) {
        var role = t.isCaptain
            ? '<span class="badge badge-active">Đội trưởng</span>'
            : '<span class="badge badge-soft">' + (t.slot === 'SUBSTITUTE' ? 'Dự bị' : 'Chính thức') + '</span>';
        var joined = t.joinedAt ? 'Tham gia ' + new Date(/Z$|[+-]\d\d:\d\d$/.test(t.joinedAt) ? t.joinedAt : t.joinedAt + 'Z').toLocaleDateString('vi-VN') : '';
        // Một nút duy nhất: đội trưởng vào Bảng điều khiển (từ đó tới mã mời, đội hình…), thành viên xem Hồ sơ đội.
        var action = t.isCaptain
            ? '<a class="btn btn-primary btn-sm" href="/team/dashboard.html?id=' + t.teamId + '">Bảng điều khiển</a>'
            : '<a class="btn btn-ghost btn-sm" href="/team/details.html?id=' + t.teamId + '">Hồ sơ đội</a>';
        return '<div class="col-12 col-md-6 col-lg-4"><div class="team-card my-team-card h-100">' +
            '<div class="top">' + logo(t) + '<div class="minw-0"><div class="name">' + Ui.escapeHtml(t.name) + '</div>' +
            '<div class="tag">[' + Ui.escapeHtml(t.tag) + '] · ' + Ui.escapeHtml(t.gameTitle) + '</div></div></div>' +
            '<div class="my-team-meta"><span class="badge badge-soft">' + t.memberCount + ' thành viên</span>' + role +
            '<span class="hint ms-auto">' + Ui.escapeHtml(joined) + '</span></div>' +
            '<div class="my-team-actions">' + action + '</div>' +
            '</div></div>';
    }

    // Phân trang phía trình duyệt (mỗi trang 6 đội); trang hiện tại giữ trong ?page= để F5 không mất.
    var PAGE_SIZE = 6;
    function currentPage() { var p = parseInt(new URLSearchParams(location.search).get('page'), 10); return p > 0 ? p : 1; }
    function pagerHtml(page, totalPages) {
        if (totalPages <= 1) return '';
        function link(p, label, disabled, active) {
            if (disabled) return '<span class="btn btn-ghost btn-sm disabled" aria-disabled="true">' + label + '</span>';
            return '<a class="btn ' + (active ? 'btn-primary' : 'btn-ghost') + ' btn-sm" href="?page=' + p + '" data-page="' + p + '"' +
                (active ? ' aria-current="page"' : '') + '>' + label + '</a>';
        }
        var html = '<nav class="dir-pager" aria-label="Phân trang các đội của tôi">' + link(page - 1, '‹ Trước', page <= 1, false);
        for (var p = 1; p <= totalPages; p++) {
            if (p === 1 || p === totalPages || Math.abs(p - page) <= 1) html += link(p, String(p), false, p === page);
            else if (Math.abs(p - page) === 2) html += '<span class="hint px-1">…</span>';
        }
        return html + link(page + 1, 'Sau ›', page >= totalPages, false) + '</nav>';
    }
    function teamsHtml(teams) {
        var totalPages = Math.max(1, Math.ceil(teams.length / PAGE_SIZE));
        var page = Math.min(currentPage(), totalPages);
        var from = (page - 1) * PAGE_SIZE;
        var slice = teams.slice(from, from + PAGE_SIZE);
        return '<div class="text-muted-etoms mb-3"><strong class="text-ink">' + teams.length + '</strong> đội' +
            (totalPages > 1 ? ' · hiển thị ' + (from + 1) + '–' + (from + slice.length) : '') + '</div>' +
            '<div class="row g-3">' + slice.map(card).join('') + '</div>' + pagerHtml(page, totalPages);
    }

    // Lời mời vào đội đang chờ trả lời (đội trưởng mời lúc tạo đội hoặc mời Free Agent).
    function invitationsHtml(list) {
        if (!Array.isArray(list) || !list.length) return '';
        return '<div class="card-etoms mb-3" id="invitations"><div class="body">' +
            '<div class="section-title m-0">Lời mời vào đội <span class="badge badge-active">' + list.length + '</span></div>' +
            '<div class="section-sub">Chấp nhận để vào đội ngay; mỗi tựa game bạn chỉ thuộc một đội.</div>' +
            list.map(function (v) {
                var when = v.invitedAt ? Ui.fmtDateTime(v.invitedAt) : '';
                return '<div class="invite-row">' + logo({ logoUrl: v.logoUrl, name: v.teamName, tag: v.teamTag }) +
                    '<div class="grow"><div class="name"><a href="/team/details.html?id=' + Number(v.teamId) + '">' + Ui.escapeHtml(v.teamName) + '</a> ' +
                    '<span class="text-muted-2">[' + Ui.escapeHtml(v.teamTag) + '] · ' + Ui.escapeHtml(v.gameTitle) + '</span></div>' +
                    '<div class="hint">Đội trưởng ' + Ui.escapeHtml(v.captainName) + (when ? ' · ' + Ui.escapeHtml(when) : '') + '</div></div>' +
                    '<div class="d-flex gap-2">' +
                    '<button type="button" class="btn btn-primary btn-sm" data-invite="' + Number(v.teamMemberId) + '" data-act="accept">Chấp nhận</button>' +
                    '<button type="button" class="btn btn-ghost btn-sm" data-invite="' + Number(v.teamMemberId) + '" data-act="decline" data-team="' + Ui.escapeHtml(v.teamName) + '">Từ chối</button>' +
                    '</div></div>';
            }).join('') + '</div></div>';
    }

    function wireInvitations(reload) {
        var flash = document.getElementById('flash');
        document.querySelectorAll('[data-invite]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                var accept = btn.dataset.act === 'accept';
                if (!accept && window.etomsConfirm && !(await window.etomsConfirm('Từ chối lời mời vào đội "' + btn.dataset.team + '"?'))) return;
                document.querySelectorAll('[data-invite]').forEach(function (b) { b.disabled = true; });
                try {
                    var res = await Api.post('/api/teams/invitations/' + btn.dataset.invite + '/' + (accept ? 'accept' : 'decline'), {});
                    Ui.showAlert(flash, 'success', res.message || (accept ? 'Đã gia nhập đội.' : 'Đã từ chối lời mời.'));
                } catch (err) {
                    Ui.showAlert(flash, 'danger', err.message);
                }
                await reload();
            });
        });
    }

    async function load() {
        var root = document.getElementById('result-root');
        try {
            var both = await Promise.all([Api.get('/api/teams/my-teams'), Api.get('/api/teams/invitations').catch(function () { return []; })]);
            var teams = both[0];
            var invites = invitationsHtml(both[1]);
            if (!Array.isArray(teams) || teams.length === 0) {
                root.innerHTML = invites + '<div class="card-etoms"><div class="empty">' +
                    '<h3>Bạn chưa tham gia đội nào</h3>' +
                    '<p>Tạo đội mới, nhập mã mời của đội trưởng, hoặc đăng tin tìm đội.</p>' +
                    '<div class="d-flex flex-wrap gap-2 justify-content-center">' +
                    '<a class="btn btn-primary" data-hide-roles="ORGANIZER ADMIN" href="/team/create.html">Tạo đội mới</a>' +
                    '<a class="btn btn-ghost" data-hide-roles="ORGANIZER ADMIN" href="/team/join.html">Gia nhập bằng mã</a>' +
                    '<a class="btn btn-ghost" href="/free-agent/index.html">Tìm đội</a></div>' +
                    '</div></div>';
                wireInvitations(load);
                return;
            }
            root.innerHTML = invites + teamsHtml(teams);
            root.querySelectorAll('.dir-pager a[data-page]').forEach(function (a) {
                a.addEventListener('click', function (e) {
                    e.preventDefault();
                    history.pushState(null, '', '?page=' + a.dataset.page);
                    load();
                    root.scrollIntoView({ block: 'start' });
                });
            });
            wireInvitations(load);
            if (location.hash === '#invitations' && document.getElementById('invitations'))
                document.getElementById('invitations').scrollIntoView({ block: 'start' });
        } catch (err) {
            root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tải được danh sách đội</h3>' +
                '<p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['PLAYER']);
        if (!me) return;
        await load();
        window.addEventListener('popstate', load);
    });
})();
