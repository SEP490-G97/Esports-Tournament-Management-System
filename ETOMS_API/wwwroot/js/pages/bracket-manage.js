(function () {
    var tournamentId = null;

    function groupByPreserveOrder(arr, keyFn) {
        var order = [], map = {};
        arr.forEach(function (item) {
            var k = keyFn(item);
            if (!map[k]) { map[k] = []; order.push(k); }
            map[k].push(item);
        });
        return order.map(function (k) { return { key: k, items: map[k] }; });
    }

    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtDateTime(iso) {
        var d = new Date(iso);
        return pad2(d.getDate()) + '/' + pad2(d.getMonth() + 1) + '/' + d.getFullYear() + ' ' + pad2(d.getHours()) + ':' + pad2(d.getMinutes());
    }
    function formatLabel(fmt) {
        return fmt === 'ROUND_ROBIN' ? 'Thể thức vòng tròn' : fmt === 'DOUBLE_ELIMINATION' ? 'Loại kép' : 'Loại trực tiếp';
    }

    function manualRowHtml(t, idx) {
        return '<div class="manual-row d-flex align-items-center gap-2" data-team="' + t.teamId + '" style="background:var(--panel-2);border-radius:10px;padding:9px 12px">' +
            '<span style="font-weight:800;color:var(--cyan);width:22px">' + (idx + 1) + '</span>' +
            '<span style="flex:1">' + Ui.escapeHtml(t.name) + ' [' + Ui.escapeHtml(t.tag) + ']</span>' +
            '<button type="button" class="btn btn-ghost" style="padding:4px 9px" data-move="-1">▲</button>' +
            '<button type="button" class="btn btn-ghost" style="padding:4px 9px" data-move="1">▼</button>' +
            '</div>';
    }

    function wireManualList(container) {
        container.querySelectorAll('[data-move]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var row = btn.closest('.manual-row');
                var list = document.getElementById('manualList');
                var rows = Array.from(list.children);
                var idx = rows.indexOf(row);
                var dir = parseInt(btn.dataset.move, 10);
                var swapWith = idx + dir;
                if (swapWith < 0 || swapWith >= rows.length) return;
                if (dir < 0) list.insertBefore(row, rows[swapWith]);
                else list.insertBefore(rows[swapWith], row);
                renumber();
            });
        });
    }

    function renumber() {
        document.querySelectorAll('#manualList .manual-row').forEach(function (r, i) {
            r.querySelector('span').textContent = (i + 1);
        });
    }

    function generateFormHtml(tree, teams) {
        var manualRows = teams.map(function (t, i) { return manualRowHtml(t, i); }).join('');
        var disabled = tree.approvedTeamCount < 2 ? 'disabled' : '';
        var warn = tree.approvedTeamCount < 2 ? '<div class="hint" style="color:var(--warn);margin-top:8px">Cần ít nhất 2 đội đã duyệt để sinh nhánh đấu.</div>' : '';

        return '<div class="card-etoms form-narrow" style="margin-bottom:28px"><div class="body">' +
            '<div class="section-title" style="margin-top:0">' + (tree.hasBracket ? 'Sinh lại nhánh đấu' : 'Sinh nhánh đấu') + '</div>' +
            '<div class="section-sub">' + tree.approvedTeamCount + ' đội đã duyệt. ' + (tree.hasBracket ? 'Sinh lại sẽ xóa cây hiện tại và dựng lại.' : 'Chọn cách xếp hạt giống rồi sinh nhánh.') + '</div>' +
            '<form id="generate-form" style="margin-top:14px">' +
            '<div class="field"><label>Cách xếp hạt giống</label>' +
            '<select name="seedingMethod" class="select" id="seedingMethod">' +
            '<option value="BY_RANKING">Theo thứ tự duyệt đơn</option>' +
            '<option value="RANDOM_DRAW">Bốc thăm ngẫu nhiên</option>' +
            '<option value="MANUAL">Thủ công (sắp xếp tay)</option>' +
            '</select></div>' +
            '<div id="manualBox" style="display:none">' +
            '<div class="hint" style="margin-bottom:10px">Dùng nút ▲▼ để sắp xếp thứ tự hạt giống (1 = hạt giống cao nhất).</div>' +
            '<div id="manualList" class="d-grid gap-2 mb-3">' + manualRows + '</div></div>' +
            '<div class="btn-row"><button type="submit" class="btn btn-primary" ' + disabled + '>' + (tree.hasBracket ? 'Sinh lại nhánh đấu' : 'Sinh nhánh đấu') + '</button></div>' +
            warn +
            '</form></div></div>' +
            (tree.hasBracket ? '<button type="button" class="btn btn-ghost" style="color:var(--gold);margin-bottom:28px" id="lock-btn">🔒 Khóa nhánh đấu</button>' : '');
    }

    function bracketContentHtml(tree) {
        if (!tree.hasBracket) {
            return '<div class="card-etoms"><div class="empty"><h3>Chưa có nhánh đấu</h3><p>Sinh nhánh đấu ở trên để bắt đầu.</p></div></div>';
        }
        var knockout = tree.matches.filter(function (m) { return m.stageType === 'KNOCKOUT'; });
        var groupMatches = tree.matches.filter(function (m) { return m.stageType === 'GROUP'; })
            .slice().sort(function (a, b) {
                return (a.groupLabel || '').localeCompare(b.groupLabel || '') || a.roundNo - b.roundNo || a.positionInRound - b.positionInRound;
            });
        var groups = groupByPreserveOrder(groupMatches, function (m) { return m.roundLabel; });

        var html = '';
        if (knockout.length > 0) {
            html += '<div class="section-title" style="margin-top:0">Sơ đồ nhánh đấu</div>' +
                '<div class="section-sub">Cây nhánh tĩnh (không kéo/thu phóng). Bản tương tác đầy đủ thuộc BRK-10, phụ trách: Nguyễn Duy Khánh.</div>' +
                '<div id="knockout-tree"></div>';
        }
        if (groupMatches.length > 0) {
            html += '<div class="section-title" style="' + (knockout.length > 0 ? 'margin-top:30px' : 'margin-top:0') + '">Lịch vòng bảng</div>' +
                '<div class="section-sub">Danh sách theo bảng/vòng (thể thức vòng tròn không vẽ dạng cây).</div>';
            groups.forEach(function (g) {
                html += '<div class="section-title" style="font-size:1.05rem;margin-top:22px">' + Ui.escapeHtml(g.key) + '</div><div class="d-grid gap-2">';
                g.items.forEach(function (m) {
                    var aColor = (m.winnerTeamId === m.teamAId && m.winnerTeamId != null) ? 'color:var(--cyan)' : '';
                    var bColor = (m.winnerTeamId === m.teamBId && m.winnerTeamId != null) ? 'color:var(--cyan)' : '';
                    var right;
                    if (m.winnerTeamId != null) right = '<span class="badge badge-active">Thắng: ' + Ui.escapeHtml(m.winnerTeamName) + '</span>';
                    else if (m.isReadyToPick && m.matchId != null) right = '<a class="btn btn-ghost" style="padding:7px 12px" href="/result/evidence.html?matchId=' + m.matchId + '">Xem/Báo cáo kết quả</a>';
                    else right = '<span class="badge badge-soft">Chờ đủ 2 đội</span>';

                    html += '<div class="card-etoms"><div class="body d-flex flex-wrap align-items-center gap-3" style="padding:14px 18px">' +
                        '<div class="d-flex align-items-center gap-2" style="flex:1;min-width:220px">' +
                        '<span style="font-weight:700;' + aColor + '">' + Ui.escapeHtml(m.teamAName || '— chờ xác định —') + '</span>' +
                        '<span style="color:var(--muted-2)">vs</span>' +
                        '<span style="font-weight:700;' + bColor + '">' + Ui.escapeHtml(m.teamBName || '— chờ xác định —') + '</span></div>' +
                        right + '</div></div>';
                });
                html += '</div>';
            });
        }
        return html;
    }

    function render(tree, teams) {
        var root = document.getElementById('manage-root');
        var lockedBadge = tree.isLocked
            ? '<span class="badge" style="background:var(--danger-bg);color:var(--danger-ink);margin-left:8px">Đã khóa ' + (tree.lockedAt ? fmtDateTime(tree.lockedAt) : '') + '</span>'
            : '';

        var html =
            '<div class="eyebrow">Ban tổ chức · Nhánh đấu</div>' +
            '<div class="page-head"><h1>' + Ui.escapeHtml(tree.tournamentName) + '</h1>' +
            '<p>' + formatLabel(tree.format) + ' · ' + tree.approvedTeamCount + ' đội đã duyệt' + lockedBadge + '</p></div>' +
            '<div class="btn-row" style="margin-bottom:22px">' +
            '<a class="btn btn-ghost" href="/registration/review.html?tournamentId=' + tree.tournamentId + '">Duyệt đơn đăng ký</a>' +
            '<a class="btn btn-ghost" href="/bracket/view.html?id=' + tree.tournamentId + '">Xem công khai</a>' +
            '<a class="btn btn-ghost" href="/bracket/tiebreaker.html?id=' + tree.tournamentId + '">Tiêu chí phân định</a>' +
            '</div>' +
            (!tree.isLocked ? generateFormHtml(tree, teams) : '') +
            bracketContentHtml(tree);

        root.innerHTML = html;

        var knockoutRoot = document.getElementById('knockout-tree');
        if (knockoutRoot) renderBracketTree(knockoutRoot, tree.matches.filter(function (m) { return m.stageType === 'KNOCKOUT'; }));

        var seedSelect = document.getElementById('seedingMethod');
        if (seedSelect) {
            seedSelect.addEventListener('change', function () {
                document.getElementById('manualBox').style.display = seedSelect.value === 'MANUAL' ? 'block' : 'none';
            });
            wireManualList(document.getElementById('manualList'));
        }

        var genForm = document.getElementById('generate-form');
        if (genForm) {
            genForm.addEventListener('submit', async function (e) {
                e.preventDefault();
                var method = seedSelect.value;
                var manualOrder = null;
                if (method === 'MANUAL') {
                    manualOrder = Array.from(document.querySelectorAll('#manualList .manual-row')).map(function (r) { return parseInt(r.dataset.team, 10); });
                }
                try {
                    var res = await Api.post('/api/tournaments/' + tournamentId + '/bracket/generate', { SeedingMethod: method, ManualOrder: manualOrder });
                    Ui.showAlert(document.getElementById('flash'), 'success', res.message);
                    await load();
                } catch (err) {
                    Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
                }
            });
        }

        var lockBtn = document.getElementById('lock-btn');
        if (lockBtn) {
            lockBtn.addEventListener('click', async function () {
                var ok = await window.etomsConfirm('Khóa nhánh đấu? Sau khi khóa sẽ không thể sửa thể thức, roster các đội bị khóa và đóng cổng đăng ký.');
                if (!ok) return;
                try {
                    var res = await Api.post('/api/tournaments/' + tournamentId + '/bracket/lock');
                    Ui.showAlert(document.getElementById('flash'), 'success', res.message);
                    await load();
                } catch (err) {
                    Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
                }
            });
        }
    }

    async function load() {
        var root = document.getElementById('manage-root');
        try {
            var tree = await Api.get('/api/tournaments/' + tournamentId + '/bracket');
            var teams = [];
            if (!tree.isLocked) {
                try { teams = await Api.get('/api/tournaments/' + tournamentId + '/bracket/approved-teams'); } catch (e) { teams = []; }
            }
            render(tree, teams);
        } catch (err) {
            if (err.status === 403) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Bạn không phải ban tổ chức giải này</h3></div></div>';
            else if (err.status === 404) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy giải đấu</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        tournamentId = Ui.qsInt('id');
        if (!tournamentId) { document.getElementById('manage-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã giải</h3></div></div>'; return; }
        var me = await Ui.requireAuth();
        if (!me) return;
        await load();
    });
})();
