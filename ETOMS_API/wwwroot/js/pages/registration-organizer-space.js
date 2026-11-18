// "Giải của tôi" (Ban tổ chức): tổng quan mọi giải mình tổ chức, lọc theo giai đoạn / game, tìm theo tên, sắp xếp,
// phân trang. Mỗi giải chỉ một lối vào: Bảng điều hành (từ đó tới duyệt đăng ký, nhánh đấu, sửa giải…).
// Trạng thái lọc giữ trên URL (?phase=&q=&game=&sort=&page=) để F5 / quay lại không mất.
(function () {
    var root = document.getElementById('organizer-tournaments');
    var flash = document.getElementById('organizer-flash');
    var PAGE_SIZE = 8;
    var all = [];
    var PHASES = [
        { key: '', label: 'Tất cả' },
        { key: 'ACTION', label: 'Cần xử lý' },
        { key: 'DRAFT', label: 'Bản nháp' },
        { key: 'REG_UPCOMING', label: 'Sắp mở đăng ký' },
        { key: 'REG_OPEN', label: 'Đang nhận đăng ký' },
        { key: 'WAITING_START', label: 'Chờ khai mạc' },
        { key: 'ONGOING', label: 'Đang diễn ra' },
        { key: 'COMPLETED', label: 'Đã kết thúc' },
        { key: 'CANCELLED', label: 'Đã hủy' }
    ];
    var PHASE_CLASS = { DRAFT: 'ph-draft', REG_UPCOMING: 'ph-upcoming', REG_OPEN: 'ph-open', WAITING_START: 'ph-wait', ONGOING: 'ph-live', COMPLETED: 'ph-done', CANCELLED: 'ph-cancel' };

    function esc(s) { return Ui.escapeHtml(s == null ? '' : String(s)); }
    function utc(iso) { return iso ? new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z') : null; }
    function d(iso) { var x = utc(iso); return x ? x.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }) : '—'; }

    function state() {
        var q = new URLSearchParams(location.search);
        return { phase: q.get('phase') || '', q: q.get('q') || '', game: q.get('game') || '', sort: q.get('sort') || 'action', page: Math.max(1, parseInt(q.get('page'), 10) || 1) };
    }
    function setState(patch, resetPage) {
        var s = Object.assign(state(), patch);
        if (resetPage) s.page = 1;
        var q = new URLSearchParams();
        if (s.phase) q.set('phase', s.phase);
        if (s.q) q.set('q', s.q);
        if (s.game) q.set('game', s.game);
        if (s.sort && s.sort !== 'action') q.set('sort', s.sort);
        if (s.page > 1) q.set('page', String(s.page));
        history.replaceState(null, '', location.pathname + (q.toString() ? '?' + q.toString() : ''));
        render();
    }

    function matchesPhase(t, phase) { return !phase || (phase === 'ACTION' ? !!t.nextAction : t.phase === phase); }
    function norm(s) { return (s || '').toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd'); }

    function filtered(s) {
        var term = norm(s.q.trim());
        var list = all.filter(function (t) {
            return matchesPhase(t, s.phase) && (!s.game || t.gameTitle === s.game) && (!term || norm(t.name).indexOf(term) >= 0);
        });
        var order = { REG_OPEN: 0, WAITING_START: 1, ONGOING: 2, DRAFT: 3, REG_UPCOMING: 4, COMPLETED: 5, CANCELLED: 6 };
        list.sort(function (a, b) {
            if (s.sort === 'name') return a.name.localeCompare(b.name, 'vi');
            if (s.sort === 'created') return utc(b.createdAt) - utc(a.createdAt);
            if (s.sort === 'start') return utc(a.startAt) - utc(b.startAt);
            // "Cần xử lý trước": có việc cần làm -> nhiều đơn chờ -> giai đoạn gần hơn -> khai mạc sớm hơn.
            return (b.nextAction ? 1 : 0) - (a.nextAction ? 1 : 0) || b.pendingRegistrations - a.pendingRegistrations ||
                (order[a.phase] - order[b.phase]) || (utc(a.startAt) - utc(b.startAt));
        });
        return list;
    }

    function renderPhases(s) {
        var box = document.getElementById('ohub-phases');
        box.innerHTML = PHASES.map(function (p) {
            var n = all.filter(function (t) { return matchesPhase(t, p.key); }).length;
            if (p.key && p.key !== 'ACTION' && n === 0) return '';
            var active = s.phase === p.key;
            return '<button type="button" role="tab" aria-selected="' + active + '" class="ohub-chip' + (active ? ' active' : '') + (p.key === 'ACTION' && n ? ' warn' : '') +
                '" data-phase="' + p.key + '">' + esc(p.label) + ' <span>' + n + '</span></button>';
        }).join('');
        box.querySelectorAll('[data-phase]').forEach(function (b) {
            b.addEventListener('click', function () { setState({ phase: b.dataset.phase }, true); });
        });
    }

    function card(t) {
        var dash = '/tournament/dashboard.html?id=' + Number(t.tournamentId);
        var pct = t.maxTeams ? Math.min(100, Math.round(t.approvedTeams / t.maxTeams * 100)) : 0;
        var banner = Ui.safeUrl(t.bannerUrl);
        var timeline = t.phase === 'REG_OPEN' ? 'Nhận đăng ký đến <strong>' + d(t.regCloseAt) + '</strong>'
            : t.phase === 'REG_UPCOMING' ? 'Mở đăng ký từ <strong>' + d(t.regOpenAt) + '</strong>'
            : (t.phase === 'ONGOING' || t.phase === 'COMPLETED') && t.endAt ? d(t.startAt) + ' – ' + d(t.endAt)
            : t.phase === 'ONGOING' ? 'Khởi tranh <strong>' + d(t.startAt) + '</strong>'
            : 'Khai mạc <strong>' + d(t.startAt) + '</strong>';
        return '<article class="ohub-card">' +
            '<a class="ohub-cover" href="' + dash + '" tabindex="-1" aria-hidden="true"' + (banner ? ' style="background-image:url(\'' + esc(banner) + '\')"' : '') + '>' +
            (banner ? '' : '<span>' + esc(t.gameTitle) + '</span>') + '</a>' +
            '<div class="ohub-body">' +
            '<div class="ohub-top"><span class="ohub-phase ' + (PHASE_CLASS[t.phase] || '') + '">' + esc(t.phaseLabel) + '</span>' +
            (t.pendingRegistrations ? '<span class="ohub-pending" title="Đơn đăng ký chờ duyệt">' + t.pendingRegistrations + ' đơn chờ duyệt</span>' : '') + '</div>' +
            '<h3 class="ohub-name"><a href="' + dash + '">' + esc(t.name) + '</a></h3>' +
            '<div class="ohub-meta">' + esc(t.gameTitle) + ' · ' + esc(t.formatLabel) + '</div>' +
            '<div class="ohub-meta">' + timeline + '</div>' +
            '<div class="ohub-teams"><div class="ohub-bar" role="progressbar" aria-valuemin="0" aria-valuemax="' + t.maxTeams + '" aria-valuenow="' + t.approvedTeams + '" aria-label="Đội đã duyệt"><span style="width:' + pct + '%"></span></div>' +
            '<span><strong>' + t.approvedTeams + '</strong>/' + t.maxTeams + ' đội</span></div>' +
            '<div class="ohub-foot">' +
            (t.nextAction ? '<span class="ohub-next">→ ' + esc(t.nextAction) + '</span>' : '<span class="ohub-next muted">Không có việc cần làm ngay</span>') +
            '<a class="btn btn-primary btn-sm" href="' + dash + '">Bảng điều hành</a></div>' +
            '</div></article>';
    }

    function pager(page, total) {
        if (total <= 1) return '';
        function link(p, label, disabled, active) {
            if (disabled) return '<span class="btn btn-ghost btn-sm disabled" aria-disabled="true">' + label + '</span>';
            return '<button type="button" class="btn ' + (active ? 'btn-primary' : 'btn-ghost') + ' btn-sm" data-page="' + p + '"' + (active ? ' aria-current="page"' : '') + '>' + label + '</button>';
        }
        var html = '<nav class="dir-pager" aria-label="Phân trang giải của tôi">' + link(page - 1, '‹ Trước', page <= 1, false);
        for (var p = 1; p <= total; p++) {
            if (p === 1 || p === total || Math.abs(p - page) <= 1) html += link(p, String(p), false, p === page);
            else if (Math.abs(p - page) === 2) html += '<span class="hint px-1">…</span>';
        }
        return html + link(page + 1, 'Sau ›', page >= total, false) + '</nav>';
    }

    function render() {
        var s = state();
        renderPhases(s);
        document.getElementById('ohub-q').value = s.q;
        document.getElementById('ohub-game').value = s.game;
        document.getElementById('ohub-sort').value = s.sort;
        if (!all.length) {
            root.innerHTML = '<div class="card-etoms ohub-empty"><div class="empty"><h3>Bạn chưa tổ chức giải nào</h3>' +
                '<p>Tạo giải đầu tiên: điền thông tin, thời gian, số ván mỗi trận rồi công bố.</p>' +
                '<a class="btn btn-primary" href="/tournament-setup/index.html">+ Tạo giải mới</a></div></div>';
            document.getElementById('ohub-summary').textContent = '';
            document.getElementById('ohub-pager').innerHTML = '';
            return;
        }
        var list = filtered(s);
        var totalPages = Math.max(1, Math.ceil(list.length / PAGE_SIZE));
        var page = Math.min(s.page, totalPages);
        var slice = list.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
        document.getElementById('ohub-summary').textContent = list.length
            ? list.length + ' giải' + (totalPages > 1 ? ' · trang ' + page + '/' + totalPages : '') : '';
        root.innerHTML = slice.length ? slice.map(card).join('')
            : '<div class="card-etoms ohub-empty"><div class="empty"><h3>Không có giải phù hợp</h3><p>Thử đổi bộ lọc hoặc từ khóa.</p>' +
              '<button type="button" class="btn btn-ghost" id="ohub-clear">Xóa bộ lọc</button></div></div>';
        var clear = document.getElementById('ohub-clear');
        if (clear) clear.addEventListener('click', function () { setState({ phase: '', q: '', game: '', sort: 'action' }, true); });
        var pg = document.getElementById('ohub-pager');
        pg.innerHTML = pager(page, totalPages);
        pg.querySelectorAll('[data-page]').forEach(function (b) {
            b.addEventListener('click', function () { setState({ page: parseInt(b.dataset.page, 10) }); window.scrollTo({ top: 0, behavior: 'smooth' }); });
        });
    }

    function wireToolbar() {
        var timer = null;
        document.getElementById('ohub-q').addEventListener('input', function (e) {
            clearTimeout(timer);
            var v = e.target.value;
            timer = setTimeout(function () { setState({ q: v.slice(0, 100) }, true); }, 250);
        });
        document.getElementById('ohub-game').addEventListener('change', function (e) { setState({ game: e.target.value }, true); });
        document.getElementById('ohub-sort').addEventListener('change', function (e) { setState({ sort: e.target.value }, true); });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ORGANIZER']);
        if (!me) return;
        Ui.consumeFlash(flash);
        try {
            var res = await Api.get('/api/organizer/tournaments');
            all = Array.isArray(res) ? res : (res.data || []);
            var games = Array.from(new Set(all.map(function (t) { return t.gameTitle; }))).sort(function (a, b) { return a.localeCompare(b, 'vi'); });
            document.getElementById('ohub-game').innerHTML = '<option value="">Mọi tựa game</option>' +
                games.map(function (g) { return '<option value="' + esc(g) + '">' + esc(g) + '</option>'; }).join('');
            wireToolbar();
            render();
        } catch (error) {
            root.innerHTML = '';
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải danh sách giải đấu.');
        }
    });
})();
