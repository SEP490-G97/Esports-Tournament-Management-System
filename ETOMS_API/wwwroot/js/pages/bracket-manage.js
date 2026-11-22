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
            '<span class="flex-grow-1">' + Ui.escapeHtml(t.name) + ' [' + Ui.escapeHtml(t.tag) + ']</span>' +
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
        // Còn nhận đăng ký thì chưa sinh nhánh (UC-20: đóng đăng ký rồi mới sinh) — máy chủ cũng chặn.
        var stillOpen = !!tree.registrationStillOpen;
        var closeAt = tree.regCloseAt ? new Date(/Z$|[+-]\d\d:\d\d$/.test(tree.regCloseAt) ? tree.regCloseAt : tree.regCloseAt + 'Z').toLocaleString('vi-VN') : '';
        var disabled = (tree.approvedTeamCount < 2 || stillOpen) ? 'disabled' : '';
        var warn = stillOpen
            ? '<div class="hint" style="color:var(--warn);margin-top:8px">Giải còn nhận đăng ký đến ' + Ui.escapeHtml(closeAt) + '. Sinh nhánh sau khi đóng đăng ký để danh sách đội đã chốt.</div>'
            : tree.approvedTeamCount < 2 ? '<div class="hint" style="color:var(--warn);margin-top:8px">Cần ít nhất 2 đội đã duyệt để sinh nhánh đấu.</div>' : '';

        return '<div class="card-etoms form-narrow" style="margin-bottom:28px"><div class="body">' +
            '<div class="section-title mt-0">' + (tree.hasBracket ? 'Sinh lại nhánh đấu' : 'Sinh nhánh đấu') + '</div>' +
            '<div class="section-sub">' + tree.approvedTeamCount + ' đội đã duyệt. ' + (tree.hasBracket ? 'Sinh lại sẽ xóa cây hiện tại và dựng lại.' : 'Chọn cách xếp hạt giống rồi sinh nhánh.') + '</div>' +
            '<form id="generate-form" style="margin-top:14px">' +
            '<div class="field"><label>Cách xếp hạt giống</label>' +
            '<select name="seedingMethod" class="select" id="seedingMethod">' +
            '<option value="BY_RANKING">Theo thứ tự duyệt đơn</option>' +
            '<option value="RANDOM_DRAW">Bốc thăm ngẫu nhiên</option>' +
            '<option value="MANUAL">Thủ công (sắp xếp tay)</option>' +
            '</select></div>' +
            '<div id="manualBox" style="display:none">' +
            '<div class="hint mb-2">Dùng nút ▲▼ để sắp xếp thứ tự hạt giống (1 = hạt giống cao nhất).</div>' +
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
        if (tree.format === 'DOUBLE_ELIMINATION' && knockout.length > 0) {
            // BRK-05 Loại kép: nhánh thắng vẽ dạng cây; nhánh thua + chung kết tổng xếp theo cột vòng.
            html += '<div class="section-sub">Sơ đồ tóm tắt; xem bản tương tác ở tab “Nhánh đấu”. Thua 2 trận mới bị loại: đội thua ở nhánh thắng rơi xuống nhánh thua.</div>' +
                '<div class="section-title mt-0">Nhánh thắng</div><div id="de-winners"></div>' +
                '<div class="section-title" style="margin-top:26px">Nhánh thua</div><div id="de-losers"></div>' +
                '<div class="section-title" style="margin-top:26px">Chung kết tổng</div><div id="de-grand"></div>';
            return html;
        }
        if (knockout.length > 0) {
            html += '<div class="section-title mt-0">Sơ đồ nhánh đấu</div>' +
                '<div class="section-sub">Sơ đồ tóm tắt; xem bản tương tác ở tab “Nhánh đấu”.</div>' +
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
                        '<span class="text-muted-2">vs</span>' +
                        '<span style="font-weight:700;' + bColor + '">' + Ui.escapeHtml(m.teamBName || '— chờ xác định —') + '</span></div>' +
                        right + '</div></div>';
                });
                html += '</div>';
            });
        }
        return html;
    }

    // Nhánh thua có vòng 1-1 (không gộp đôi như cây loại trực tiếp) nên xếp mỗi vòng một cột.
    function renderRoundColumns(container, matches) {
        if (!container) return;
        var byRound = {};
        matches.forEach(function (m) { (byRound[m.roundNo] = byRound[m.roundNo] || []).push(m); });
        var rounds = Object.keys(byRound).map(Number).sort(function (a, b) { return a - b; });
        if (!rounds.length) { container.innerHTML = '<div class="hint">Không có trận.</div>'; return; }
        var name = function (m, side) {
            var id = side === 'A' ? m.teamAId : m.teamBId, nm = side === 'A' ? m.teamAName : m.teamBName;
            var win = m.winnerTeamId != null && m.winnerTeamId === id;
            return '<div class="bn' + (win ? ' win' : '') + '">' + Ui.escapeHtml(nm || 'Chờ xác định') + '</div>';
        };
        container.innerHTML = '<div class="de-cols">' + rounds.map(function (r) {
            var list = byRound[r].slice().sort(function (a, b) { return a.positionInRound - b.positionInRound; });
            // Tiêu đề khối đã ghi "Nhánh thua" nên cột chỉ cần "Vòng 2", "Chung kết nhánh thua"...
            var title = (list[0].roundLabel || ('Vòng ' + r)).replace(/ · Trận \d+$/, '').replace(/^Nhánh (thắng|thua) · /, '');
            return '<div class="de-col"><div class="de-col-title">' + Ui.escapeHtml(title) + '</div>' + list.map(function (m) {
                var link = m.isReadyToPick && m.matchId != null
                    ? '<a class="badge badge-soft mt-1" style="text-decoration:none" href="/result/evidence.html?matchId=' + m.matchId + '">Xem/Báo cáo kết quả →</a>' : '';
                return '<div class="bracket-card">' + name(m, 'A') + name(m, 'B') + link + '</div>';
            }).join('') + '</div>';
        }).join('') + '</div>';
    }

    function render(tree, teams) {
        var root = document.getElementById('manage-root');
        var lockedBadge = tree.isLocked
            ? '<span class="badge" style="background:var(--danger-bg);color:var(--danger-ink);margin-left:8px">Đã khóa ' + (tree.lockedAt ? fmtDateTime(tree.lockedAt) : '') + '</span>'
            : '';

        // Tiêu đề cố định trong HTML (menu của giải chèn ngay dưới) — chỉ điền chữ.
        document.getElementById('page-title').textContent = tree.tournamentName;
        document.getElementById('page-sub').innerHTML = Ui.escapeHtml(formatLabel(tree.format) + ' · ' + tree.approvedTeamCount + ' đội đã duyệt') + lockedBadge;

        var html =
            '<div class="btn-row mb-4">' +
            // Tiêu chí phân định khi bằng điểm (BRK-07) chỉ dùng cho bảng điểm: vòng tròn / vòng bảng.
            (tree.format === 'ROUND_ROBIN' || tree.format === 'HYBRID'
                ? '<a class="btn btn-ghost" href="/bracket/tiebreaker.html?id=' + tree.tournamentId + '">Tiêu chí phân định (khi bằng điểm)</a>' : '') +
            (tree.hasBracket && !tree.isLocked ? '<a class="btn btn-primary" href="/tournament/bracket.html?id=' + tree.tournamentId + '">Đổi chỗ đội (kéo thả trên sơ đồ)</a>' : '') +
            (tree.canUnlock ? '<button type="button" class="btn btn-ghost" id="unlock-btn">Mở khóa nhánh</button>' : '') +
            '</div>' +
            (!tree.isLocked ? generateFormHtml(tree, teams) : '') +
            bracketContentHtml(tree);

        root.innerHTML = html;

        var knockoutRoot = document.getElementById('knockout-tree');
        if (knockoutRoot) renderBracketTree(knockoutRoot, tree.matches.filter(function (m) { return m.stageType === 'KNOCKOUT'; }));
        var deWinners = document.getElementById('de-winners');
        if (deWinners) {
            var inStage = function (no) { return tree.matches.filter(function (m) { return m.stageType === 'KNOCKOUT' && m.stageNo === no; }); };
            renderBracketTree(deWinners, inStage(1));
            renderRoundColumns(document.getElementById('de-losers'), inStage(2));
            renderRoundColumns(document.getElementById('de-grand'), inStage(3));
        }

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
                await runOnce(genForm.querySelector('button[type=submit]'), 'Đang sinh nhánh…',
                    function () { return Api.post('/api/tournaments/' + tournamentId + '/bracket/generate', { SeedingMethod: method, ManualOrder: manualOrder }); });
            });
        }

        var lockBtn = document.getElementById('lock-btn');
        if (lockBtn) {
            lockBtn.addEventListener('click', async function () {
                var ok = await window.etomsConfirm('Khóa nhánh đấu? Sau khi khóa, khách xem được nhánh, roster các đội bị khóa và cổng đăng ký đóng. Vẫn mở khóa lại được nếu chưa có trận nào bắt đầu.');
                if (!ok) return;
                await runOnce(lockBtn, 'Đang khóa…', function () { return Api.post('/api/tournaments/' + tournamentId + '/bracket/lock'); });
            });
        }

        var unlockBtn = document.getElementById('unlock-btn');
        if (unlockBtn) {
            unlockBtn.addEventListener('click', async function () {
                var ok = await window.etomsConfirm('Mở khóa nhánh đấu? Khách sẽ không xem được nhánh cho tới khi khóa lại.');
                if (!ok) return;
                await runOnce(unlockBtn, 'Đang mở khóa…', function () { return Api.post('/api/tournaments/' + tournamentId + '/bracket/unlock'); });
            });
        }
    }

    // Khóa nút suốt lúc gửi + tải lại cây (bấm liên tục không gửi thêm yêu cầu). Thành công thì load() vẽ lại
    // cả khu vực nên nút mới tự mở; lỗi thì mở lại nút cũ để thử lại.
    async function runOnce(btn, busyText, call) {
        if (!btn || btn.dataset.busy === '1') return;
        var oldText = btn.textContent;
        btn.dataset.busy = '1'; btn.disabled = true; btn.textContent = busyText;
        var flash = document.getElementById('flash');
        try {
            var res = await call();
            Ui.showAlert(flash, 'success', res.message);
            await load();
        } catch (err) {
            Ui.showAlert(flash, 'danger', err.message);
        } finally {
            if (btn.isConnected) { btn.dataset.busy = ''; btn.disabled = false; btn.textContent = oldText; }
        }
    }

    async function load() {
        var root = document.getElementById('manage-root');
        try {
            // approved-teams chỉ trả cho Ban tổ chức của giải (403 với người khác) — dùng luôn để kiểm quyền,
            // vì dữ liệu nhánh (/bracket) là công khai.
            var results = await Promise.all([
                Api.get('/api/tournaments/' + tournamentId + '/bracket'),
                Api.get('/api/tournaments/' + tournamentId + '/bracket/approved-teams')
            ]);
            render(results[0], results[1]);
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
