(function () {
    var PAGE_SIZE = 12;

    function statusBadge(status) {
        return status === 'ACTIVE'
            ? '<span class="badge badge-active">Hoạt động</span>'
            : '<span class="badge" style="background:var(--danger-bg);color:var(--danger-ink)">Giải tán</span>';
    }

    function emptyCard(title, text, actionHtml) {
        return '<div class="card-etoms"><div class="empty"><h3>' + title + '</h3><p>' + text + '</p>' + (actionHtml || '') + '</div></div>';
    }

    // Link sang trang khác giữ nguyên bộ lọc hiện tại; trang 1 thì bỏ tham số Page cho URL gọn.
    function pageHref(page) {
        var params = new URLSearchParams(location.search);
        if (page > 1) params.set('Page', String(page)); else params.delete('Page');
        var qs = params.toString();
        return location.pathname + (qs ? '?' + qs : '');
    }

    function renderPager(page, totalPages) {
        if (totalPages <= 1) return '';
        function link(p, label, disabled, active) {
            if (disabled) return '<span class="btn btn-ghost btn-sm disabled" aria-disabled="true">' + label + '</span>';
            return '<a class="btn ' + (active ? 'btn-primary' : 'btn-ghost') + ' btn-sm" href="' + Ui.escapeHtml(pageHref(p)) + '"' +
                (active ? ' aria-current="page"' : '') + '>' + label + '</a>';
        }
        var html = '<nav class="dir-pager" aria-label="Phân trang danh sách đội">' + link(page - 1, '‹ Trước', page <= 1, false);
        for (var p = 1; p <= totalPages; p++) {
            if (p === 1 || p === totalPages || Math.abs(p - page) <= 1) html += link(p, String(p), false, p === page);
            else if (Math.abs(p - page) === 2) html += '<span class="hint px-1">…</span>';
        }
        return html + link(page + 1, 'Sau ›', page >= totalPages, false) + '</nav>';
    }

    function renderList(data, hasFilter) {
        var root = document.getElementById('result-root');
        if (!data.items.length) {
            root.innerHTML = hasFilter
                ? emptyCard('Không tìm thấy đội phù hợp', 'Thử đổi từ khóa hoặc bộ lọc khác.', '<a class="btn btn-ghost" href="/team/index.html">Xóa bộ lọc</a>')
                : emptyCard('Chưa có đội nào', 'Hãy tạo đội đầu tiên để bắt đầu.', '<a class="btn btn-primary" data-hide-roles="ORGANIZER ADMIN" href="/team/create.html">Tạo đội mới</a>');
            return;
        }
        var from = (data.page - 1) * data.pageSize + 1;
        var to = from + data.items.length - 1;
        var html = '<div style="color:var(--muted);font-size:.86rem;margin-bottom:16px"><strong class="text-ink">' + data.total + '</strong> đội phù hợp' +
            (data.totalPages > 1 ? ' · đang xem ' + from + '–' + to + ' (trang ' + data.page + '/' + data.totalPages + ')' : '') + '</div>';
        html += '<div class="row g-3">';
        data.items.forEach(function (t) {
            var logo = t.logoUrl
                ? '<img class="team-logo" src="' + Ui.escapeHtml(t.logoUrl) + '" alt="Logo ' + Ui.escapeHtml(t.name) + '" />'
                : '<span class="team-logo">' + Ui.escapeHtml(t.tag && t.tag.length > 0 ? t.tag.substring(0, 1) : '?') + '</span>';
            html +=
                '<div class="col-12 col-md-6 col-lg-4">' +
                '<a class="team-card d-block h-100" href="/team/details.html?id=' + Number(t.teamId) + '">' +
                '<div class="top">' + logo + '<div><div class="name">' + Ui.escapeHtml(t.name) + '</div><div class="tag">[' + Ui.escapeHtml(t.tag) + '] · ' + Ui.escapeHtml(t.gameTitle) + '</div></div></div>' +
                '<div class="meta"><span class="cap" title="Đội trưởng: ' + Ui.escapeHtml(t.captainName) + '">' + Ui.escapeHtml(t.captainName) + '</span><span class="chips">' +
                '<span class="badge badge-soft">' + Number(t.memberCount) + ' thành viên</span> ' + statusBadge(t.status) +
                '</span></div></a></div>';
        });
        html += '</div>' + renderPager(data.page, data.totalPages);
        root.innerHTML = html;
    }

    async function populateGameOptions(selectedGameId) {
        var select = document.getElementById('game-select');
        try {
            var games = await Api.get('/api/teams/games');
            games.forEach(function (g) {
                var opt = document.createElement('option');
                opt.value = g.gameId;
                opt.textContent = g.title;
                if (selectedGameId && String(g.gameId) === String(selectedGameId)) opt.selected = true;
                select.appendChild(opt);
            });
        } catch (e) { /* dropdown trống nếu lỗi, không chặn trang */ }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        Ui.consumeFlash(document.getElementById('flash'));

        var params = new URLSearchParams(location.search);
        var q = (params.get('Q') || '').trim();
        var gameId = params.get('GameId') || '';
        var status = params.get('Status') || 'ACTIVE';
        var sort = params.get('Sort') || 'newest';
        var pageRaw = params.get('Page') || '1';
        var hasFilter = !!q || !!gameId || status !== 'ACTIVE';

        var form = document.getElementById('filter-form');
        var qInput = form.querySelector('[name="Q"]');
        qInput.value = q;
        form.querySelector('[name="Status"]').value = status;
        form.querySelector('[name="Sort"]').value = sort;
        document.getElementById('clear-filter').style.display = hasFilter ? '' : 'none';

        // Đồng bộ với mọi thanh lọc khác: ô chọn đổi là lọc ngay (onchange), ô tìm kiếm tự lọc sau khi ngừng gõ ~0,5 giây
        // (không cần nút "Tìm"). Trang tải lại theo URL nên ghi nhớ để đặt lại con trỏ cuối ô tìm kiếm.
        var typingTimer = null;
        qInput.addEventListener('input', function () {
            clearTimeout(typingTimer);
            typingTimer = setTimeout(function () {
                try { sessionStorage.setItem('team-q-focus', '1'); } catch (e) { /* bỏ qua */ }
                if (form.requestSubmit) form.requestSubmit(); else form.submit();
            }, 500);
        });
        try {
            if (sessionStorage.getItem('team-q-focus')) {
                sessionStorage.removeItem('team-q-focus');
                qInput.focus();
                qInput.setSelectionRange(qInput.value.length, qInput.value.length);
            }
        } catch (e) { /* bỏ qua */ }

        // Kiểm tra trước khi gửi: từ khóa tối đa 100 ký tự. Gửi form luôn về trang 1 (form không có ô Page).
        form.addEventListener('submit', function (e) {
            Ui.clearFieldErrors(form);
            qInput.value = qInput.value.trim();
            if (qInput.value.length > 100) {
                e.preventDefault();
                Ui.applyFieldErrors(form, { Q: ['Từ khóa tìm kiếm tối đa 100 ký tự.'] });
                qInput.focus();
            }
        });

        await populateGameOptions(gameId);

        var root = document.getElementById('result-root');
        // URL sửa tay sai số trang (chữ, 0, số âm) → báo ngay, không gọi API.
        if (!/^\d{1,6}$/.test(pageRaw) || parseInt(pageRaw, 10) < 1) {
            root.innerHTML = emptyCard('Số trang không hợp lệ', 'Số trang phải là số nguyên từ 1 trở lên.',
                '<a class="btn btn-ghost" href="' + Ui.escapeHtml(pageHref(1)) + '">Về trang 1</a>');
            return;
        }

        var query = new URLSearchParams();
        if (q) query.set('q', q);
        if (gameId) query.set('gameId', gameId);
        query.set('status', status);
        query.set('sort', sort);
        query.set('page', pageRaw);
        query.set('pageSize', String(PAGE_SIZE));

        root.innerHTML = '<div class="card-etoms"><div class="empty is-loading"><h3>Đang tải danh sách đội...</h3></div></div>';
        try {
            var res = await Api.get('/api/teams?' + query.toString());
            var data = res.data;
            // Server đưa trang vượt quá về trang cuối → sửa URL cho khớp trang đang xem.
            if (String(data.page) !== pageRaw) history.replaceState(null, '', pageHref(data.page));
            renderList(data, hasFilter);
        } catch (err) {
            if (err.status === 400) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                root.innerHTML = emptyCard('Bộ lọc chưa hợp lệ', Ui.escapeHtml(err.message || 'Tham số tìm kiếm không hợp lệ.'),
                    '<a class="btn btn-ghost" href="/team/index.html">Xóa bộ lọc</a>');
            } else {
                root.innerHTML = emptyCard('Không tải được danh sách đội', Ui.escapeHtml(err.message || 'Vui lòng thử lại sau.'));
            }
        }
    });
})();
