// UC-43 — đọc nhật ký (AuditLog). /admin/audit-log.html: quản trị viên, toàn hệ thống, có lọc người thực hiện / giải / ngày.
// /tournament/audit-log.html?id=: Ban tổ chức đọc nhật ký giải mình. Chỉ đọc — không có nút sửa hay xóa.
(function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var isAdmin = location.pathname.toLowerCase().indexOf('/admin/') === 0;
    var tournamentId = null, page = 1, flash, form, actionsLoaded = false;
    var OBJECT = {
        ACCOUNT: 'Tài khoản', ORGANIZATION: 'Đơn vị tổ chức', TOURNAMENT: 'Giải đấu', REGISTRATION: 'Đơn đăng ký', MATCH: 'Trận đấu',
        DISPUTE: 'Tranh chấp', TEAM: 'Đội', PENALTY: 'Thẻ phạt', GAME: 'Tựa game', BANNER: 'Biểu ngữ', ANNOUNCEMENT: 'Thông báo hệ thống'
    };
    function objectLabel(type) { return OBJECT[String(type || '').toUpperCase()] || 'Đối tượng'; }

    function fmt(iso) {
        var d = new Date(/Z$/.test(iso) ? iso : iso + 'Z');
        return isNaN(d.getTime()) ? '' : d.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });
    }

    function showEmpty(title, text) {
        document.getElementById('log-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>' + esc(title) + '</h3><p>' + esc(text || '') + '</p></div></div>';
    }

    function query() {
        var p = new URLSearchParams();
        if (form.actionType.value) p.set('actionType', form.actionType.value);
        if (isAdmin) {
            if (form.keyword.value.trim()) p.set('keyword', form.keyword.value.trim());
            if (form.tournamentId.value) p.set('tournamentId', form.tournamentId.value);
            if (form.from.value) p.set('from', form.from.value);
            if (form.to.value) p.set('to', form.to.value);
        }
        p.set('page', page);
        p.set('pageSize', 50);
        return p.toString();
    }

    function render(d) {
        if (!actionsLoaded) {
            form.actionType.insertAdjacentHTML('beforeend', d.actions.map(function (a) {
                return '<option value="' + esc(a.code) + '">' + esc(a.label) + '</option>';
            }).join(''));
            actionsLoaded = true;
        }
        if (!d.items.length) { showEmpty('Không có dòng nhật ký nào', 'Thử bỏ bớt điều kiện lọc.'); return; }
        var pages = Math.max(1, Math.ceil(d.total / d.pageSize));
        var rows = d.items.map(function (a) {
            // Giá trị trước/sau là dữ liệu ghi nguyên văn lúc thao tác — để trong mục thu gọn.
            var change = (a.beforeValue || a.afterValue) ? '<details class="hint"><summary>Dữ liệu ghi nhận</summary>' +
                (a.beforeValue ? '<div>Trước: ' + esc(a.beforeValue) + '</div>' : '') +
                (a.afterValue ? '<div>Sau: ' + esc(a.afterValue) + '</div>' : '') + '</details>' : '';
            return '<tr><td class="text-nowrap">' + fmt(a.createdAt) + '</td>' +
                '<td><strong>' + esc(a.actionLabel) + '</strong><div class="hint">' + esc(objectLabel(a.objectType)) + (a.objectId ? ' #' + a.objectId : '') + '</div></td>' +
                '<td>' + esc(a.actorName) + (a.actorEmail ? '<div class="hint">' + esc(a.actorEmail) + '</div>' : '') + '</td>' +
                (isAdmin ? '<td>' + (a.tournamentId ? '<a href="/bracket/view.html?id=' + a.tournamentId + '">' + esc(a.tournamentName || ('#' + a.tournamentId)) + '</a>' : '—') + '</td>' : '') +
                '<td>' + esc(a.reason || '') + change + '</td></tr>';
        }).join('');
        document.getElementById('log-root').innerHTML = '<section class="card-etoms"><div class="body">' +
            '<div class="section-title mt-0">' + d.total + ' dòng nhật ký</div>' +
            '<div class="table-responsive"><table class="data-table"><thead><tr><th>Thời điểm</th><th>Thao tác</th><th>Người thực hiện</th>' +
            (isAdmin ? '<th>Giải</th>' : '') + '<th>Lý do / thay đổi</th></tr></thead><tbody>' + rows + '</tbody></table></div>' +
            (pages > 1 ? '<div class="btn-row mt-3 align-items-center"><button type="button" class="btn btn-ghost btn-sm" data-page="' + (page - 1) + '"' + (page <= 1 ? ' disabled' : '') + '>Trang trước</button>' +
                '<span>Trang ' + page + '/' + pages + '</span><button type="button" class="btn btn-ghost btn-sm" data-page="' + (page + 1) + '"' + (page >= pages ? ' disabled' : '') + '>Trang sau</button></div>' : '') +
            '</div></section>';
    }

    async function load() {
        Ui.clearFieldErrors(form);
        var url = isAdmin ? '/api/admin/audit-logs?' + query() : '/api/tournaments/' + tournamentId + '/audit-log?' + query();
        try {
            render((await Api.get(url)).data);
        } catch (err) {
            Ui.applyFieldErrors(form, err.fieldErrors);
            showEmpty(err.status === 403 ? 'Bạn không xem được nhật ký này' : 'Không tải được nhật ký', err.message);
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        flash = document.getElementById('flash');
        form = document.getElementById('log-filter');
        var me = await Ui.requireAuth(isAdmin ? ['ADMIN'] : ['ORGANIZER', 'ADMIN']);
        if (!me) return;
        if (!isAdmin) {
            tournamentId = Ui.qsInt('id');
            if (!tournamentId || tournamentId < 1) { showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.'); return; }
        }
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            if (isAdmin && form.keyword.value.trim().length > 100) { Ui.applyFieldErrors(form, { keyword: 'Bộ lọc không hợp lệ.' }); return; }
            if (isAdmin && form.from.value && form.to.value && form.from.value > form.to.value) { Ui.applyFieldErrors(form, { from: 'Bộ lọc không hợp lệ.' }); return; }
            page = 1; load();
        });
        document.getElementById('log-root').addEventListener('click', function (e) {
            var b = e.target.closest('[data-page]');
            if (!b || b.disabled) return;
            page = parseInt(b.dataset.page, 10); load();
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
        await load();
    });
})();
