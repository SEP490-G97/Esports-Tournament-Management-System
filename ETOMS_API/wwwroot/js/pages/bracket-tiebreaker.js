// BRK-07: BTC xếp lại thứ tự ưu tiên 4 tiêu chí phân định khi bằng điểm vòng bảng.
(function () {
    var tournamentId = null;

    function rowHtml(label, idx) {
        return '<div class="manual-row d-flex align-items-center gap-2" data-label="' + Ui.escapeHtml(label) + '" ' +
            'style="background:var(--panel-2);border-radius:10px;padding:10px 14px;margin-bottom:8px">' +
            '<span style="font-weight:800;color:var(--cyan);width:22px">' + (idx + 1) + '</span>' +
            '<span style="flex:1">' + Ui.escapeHtml(label) + '</span>' +
            '<button type="button" class="btn btn-ghost" style="padding:4px 9px" data-move="-1" title="Lên">▲</button>' +
            '<button type="button" class="btn btn-ghost" style="padding:4px 9px" data-move="1" title="Xuống">▼</button>' +
            '</div>';
    }

    function renumber(list) {
        Array.from(list.children).forEach(function (row, i) {
            row.querySelector('span').textContent = (i + 1);
        });
    }

    function wireReorder(list) {
        list.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-move]');
            if (!btn) return;
            var row = btn.closest('.manual-row');
            var rows = Array.from(list.children);
            var idx = rows.indexOf(row);
            var dir = parseInt(btn.dataset.move, 10);
            var swapWith = idx + dir;
            if (swapWith < 0 || swapWith >= rows.length) return;
            if (dir < 0) list.insertBefore(row, rows[swapWith]);
            else list.insertBefore(rows[swapWith], row);
            renumber(list);
        });
    }

    function render(config) {
        var root = document.getElementById('tb-root');
        var badge = config.isDefault
            ? '<span class="badge badge-soft" style="margin-bottom:12px;display:inline-block">Đang dùng thứ tự mặc định</span>'
            : '<span class="badge badge-active" style="margin-bottom:12px;display:inline-block">Giải này đã tùy chỉnh</span>';
        root.innerHTML = badge +
            '<div id="tb-list"></div>' +
            '<div class="btn-row" style="margin-top:16px">' +
            '<button type="button" class="btn btn-primary" id="tb-save">Lưu thứ tự</button>' +
            '</div>';
        var list = document.getElementById('tb-list');
        config.order.forEach(function (label, i) { list.insertAdjacentHTML('beforeend', rowHtml(label, i)); });
        wireReorder(list);

        document.getElementById('tb-save').addEventListener('click', async function () {
            var order = Array.from(list.children).map(function (r) { return r.dataset.label; });
            try {
                var res = await Api.put('/api/tournaments/' + tournamentId + '/bracket/tiebreaker', { Order: order });
                Ui.showAlert(document.getElementById('flash'), 'success', res.message);
                load();
            } catch (err) {
                Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
            }
        });
    }

    async function load() {
        var root = document.getElementById('tb-root');
        try {
            var config = await Api.get('/api/tournaments/' + tournamentId + '/bracket/tiebreaker');
            render(config);
        } catch (err) {
            root.innerHTML = '<div class="empty"><h3>Không tìm thấy giải đấu.</h3></div>';
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        tournamentId = Ui.qsInt('id');
        if (!tournamentId) {
            document.getElementById('tb-root').innerHTML = '<div class="empty"><h3>Thiếu mã giải đấu (?id=).</h3></div>';
            return;
        }
        load();
    });
})();
