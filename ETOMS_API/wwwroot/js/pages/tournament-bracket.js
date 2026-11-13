(function () {
    // Kích thước ô trận trên sơ đồ (px, trước khi phóng to).
    var W = 220, H = 88, COL_GAP = 64, ROW_GAP = 18, STAGE_GAP = 56, TITLE_H = 44;
    var MIN_ZOOM = 0.3, MAX_ZOOM = 2, REFRESH_MS = 30000;
    var MOBILE = window.matchMedia('(max-width: 767.98px)');

    var tournamentId = null;
    var board = null;
    var standings = null;
    var state = { mode: null, userMode: null, round: 'all', team: null, detail: null, zoom: 1, panX: 0, panY: 0, fitted: false };
    var layout = null;   // { nodes: {nodeId: {x,y,node,stage}}, width, height }
    var timer = null;

    function esc(s) { return Ui.escapeHtml(s == null ? '' : String(s)); }
    function val(n) { return n === null || n === undefined ? '' : n; }

    function showEmpty(title, message) {
        document.getElementById('bracket-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + esc(title) + '</h3><p>' + esc(message) + '</p></div></div>';
    }

    function formatTime(value) {
        if (!value) return '';
        var d = new Date(/Z$/.test(value) ? value : value + 'Z');
        return d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' });
    }

    function allNodes() {
        var list = [];
        board.stages.forEach(function (s) { s.nodes.forEach(function (n) { list.push({ stage: s, node: n }); }); });
        return list;
    }

    function hasTeam(n, teamId) { return teamId !== null && (n.teamA.teamId === teamId || n.teamB.teamId === teamId); }
    function roundKey(stage, roundNo) { return stage.stageNo + ':' + roundNo; }
    function inRound(stage, n) { return state.round === 'all' || state.round === roundKey(stage, n.roundNo); }

    function headText(n) {
        if (n.isBye) return n.label;
        var extra = n.statusLabel || '';
        if (n.status === 'PENDING' && n.scheduledAt) extra = formatTime(n.scheduledAt);
        return n.label + (extra ? ' · ' + extra : '');
    }

    function teamRow(n, slot, score, extraClass) {
        var id = slot.teamId;
        var win = id !== null && id !== undefined && n.winnerTeamId === id && !n.isBye;
        var sel = id !== null && id !== undefined && state.team === id;
        return '<button type="button" class="bk-team' + (win ? ' win' : '') + (sel ? ' sel' : '') + (extraClass || '') + '"' +
            (id ? ' data-team="' + id + '" aria-pressed="' + sel + '" title="Xem hành trình của ' + esc(slot.name) + '"' : ' disabled') + '>' +
            '<span class="nm">' + esc(slot.name) + '</span><span class="sc">' + (score === null || score === undefined ? '' : score) + '</span></button>';
    }

    // ===== Sơ đồ cây (BRK-10) =====
    function computeLayout() {
        var positions = {};
        var offsetY = 0, width = 0;
        var titles = [];
        board.stages.forEach(function (stage) {
            var byId = {}, children = {};
            stage.nodes.forEach(function (n) { byId[n.nodeId] = n; });
            stage.nodes.forEach(function (n) {
                if (n.parentNodeId && byId[n.parentNodeId]) (children[n.parentNodeId] = children[n.parentNodeId] || []).push(n);
            });
            Object.keys(children).forEach(function (k) { children[k].sort(function (a, b) { return a.position - b.position; }); });
            var minRound = Math.min.apply(null, stage.nodes.map(function (n) { return n.roundNo; }));
            var maxRound = Math.max.apply(null, stage.nodes.map(function (n) { return n.roundNo; }));
            var roots = stage.nodes.filter(function (n) { return !n.parentNodeId || !byId[n.parentNodeId]; })
                .sort(function (a, b) {
                    if (a.isThirdPlace !== b.isThirdPlace) return a.isThirdPlace ? 1 : -1;
                    return b.roundNo - a.roundNo || a.position - b.position;
                });
            var leaf = 0;
            var top = offsetY + TITLE_H;
            function place(n) {
                var kids = children[n.nodeId] || [];
                var y;
                if (!kids.length) { y = leaf * (H + ROW_GAP); leaf++; }
                else {
                    var sum = 0;
                    kids.forEach(function (k) { sum += place(k); });
                    y = sum / kids.length;
                }
                positions[n.nodeId] = { x: (n.roundNo - minRound) * (W + COL_GAP), y: top + y, node: n, stage: stage };
                return y;
            }
            roots.forEach(function (r) { if (r.isThirdPlace) leaf += 0.6; place(r); });
            titles.push({ text: stage.label, y: offsetY, rounds: stage.rounds.map(function (r) { return { label: r.label, x: (r.roundNo - minRound) * (W + COL_GAP) }; }) });
            width = Math.max(width, (maxRound - minRound + 1) * (W + COL_GAP) - COL_GAP);
            offsetY = top + leaf * (H + ROW_GAP) - ROW_GAP + STAGE_GAP;
        });
        return { nodes: positions, width: width, height: Math.max(0, offsetY - STAGE_GAP), titles: titles };
    }

    function treeHtml() {
        layout = computeLayout();
        var paths = '';
        Object.keys(layout.nodes).forEach(function (id) {
            var p = layout.nodes[id];
            var parent = p.node.parentNodeId && layout.nodes[p.node.parentNodeId];
            if (!parent) return;
            var x1 = p.x + W, y1 = p.y + H / 2, x2 = parent.x, y2 = parent.y + H / 2, mid = x1 + COL_GAP / 2;
            var hl = state.team !== null && hasTeam(p.node, state.team) && hasTeam(parent.node, state.team);
            var dim = (state.team !== null && !hl) || (state.round !== 'all' && !(inRound(p.stage, p.node) || inRound(parent.stage, parent.node)));
            paths += '<path class="' + (hl ? 'hl' : dim ? 'dim' : '') + '" d="M' + x1 + ' ' + y1 + ' H' + mid + ' V' + y2 + ' H' + x2 + '" />';
        });
        var titles = layout.titles.map(function (t) {
            return '<div class="bk-stage-title" style="top:' + t.y + 'px">' + esc(t.text) + '</div>' +
                t.rounds.map(function (r) { return '<div class="bk-round-title" style="top:' + (t.y + 20) + 'px;left:' + r.x + 'px">' + esc(r.label) + '</div>'; }).join('');
        }).join('');
        var boxes = Object.keys(layout.nodes).map(function (id) {
            var p = layout.nodes[id], n = p.node;
            var cls = 'bk-match' + (n.isThirdPlace ? ' third' : '') + (n.isBye ? ' bye' : '');
            if (state.team !== null) cls += hasTeam(n, state.team) ? ' hl' : ' dim';
            else if (!inRound(p.stage, n)) cls += ' dim';
            return '<div class="' + cls + '" data-node="' + n.nodeId + '" style="left:' + p.x + 'px;top:' + p.y + 'px">' +
                '<button type="button" class="bk-head" data-detail="' + n.nodeId + '" title="Xem chi tiết ' + esc(n.label) + '">' + esc(headText(n)) + '</button>' +
                teamRow(n, n.teamA, n.isBye ? null : n.scoreA) + teamRow(n, n.teamB, n.isBye ? null : n.scoreB) + '</div>';
        }).join('');
        return '<div class="bk-viewport" id="bk-viewport" tabindex="0" aria-label="Sơ đồ nhánh đấu. Kéo hoặc dùng phím mũi tên để di chuyển, phím cộng trừ để phóng to thu nhỏ.">' +
            '<div class="bk-canvas" id="bk-canvas" style="width:' + layout.width + 'px;height:' + layout.height + 'px">' +
            '<svg class="bk-lines" width="' + layout.width + '" height="' + layout.height + '">' + paths + '</svg>' + titles + boxes +
            '</div></div>';
    }

    function applyTransform() {
        var canvas = document.getElementById('bk-canvas');
        if (canvas) canvas.style.transform = 'translate(' + state.panX + 'px,' + state.panY + 'px) scale(' + state.zoom + ')';
        var label = document.getElementById('zoom-label');
        if (label) label.textContent = Math.round(state.zoom * 100) + '%';
    }

    function zoomAt(factor, cx, cy) {
        var z = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, state.zoom * factor));
        state.panX = cx - (cx - state.panX) * (z / state.zoom);
        state.panY = cy - (cy - state.panY) * (z / state.zoom);
        state.zoom = z;
        applyTransform();
    }

    // Đưa một vùng (mặc định cả sơ đồ) vào giữa khung nhìn.
    function fitTo(box) {
        var vp = document.getElementById('bk-viewport');
        if (!vp || !layout) return;
        var area = box || { x: 0, y: 0, w: layout.width, h: layout.height };
        var pad = 24;
        var z = Math.min((vp.clientWidth - pad * 2) / Math.max(area.w, 1), (vp.clientHeight - pad * 2) / Math.max(area.h, 1), 1);
        state.zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, z));
        state.panX = (vp.clientWidth - area.w * state.zoom) / 2 - area.x * state.zoom;
        state.panY = Math.max(pad, (vp.clientHeight - area.h * state.zoom) / 2) - area.y * state.zoom;
        applyTransform();
    }

    function roundBox() {
        var xs = [], ys = [];
        Object.keys(layout.nodes).forEach(function (id) {
            var p = layout.nodes[id];
            if (state.round !== 'all' && inRound(p.stage, p.node)) { xs.push(p.x); ys.push(p.y); }
        });
        if (!xs.length) return null;
        var x = Math.min.apply(null, xs), y = Math.min.apply(null, ys);
        return { x: x, y: y, w: Math.max.apply(null, xs) - x + W, h: Math.max.apply(null, ys) - y + H };
    }

    function wireViewport() {
        var vp = document.getElementById('bk-viewport');
        if (!vp) return;
        var drag = null;
        vp.addEventListener('pointerdown', function (e) {
            if (e.target.closest('button')) return;
            drag = { x: e.clientX, y: e.clientY, panX: state.panX, panY: state.panY };
            vp.setPointerCapture(e.pointerId);
            vp.classList.add('dragging');
        });
        vp.addEventListener('pointermove', function (e) {
            if (!drag) return;
            state.panX = drag.panX + (e.clientX - drag.x);
            state.panY = drag.panY + (e.clientY - drag.y);
            applyTransform();
        });
        function end() { drag = null; vp.classList.remove('dragging'); }
        vp.addEventListener('pointerup', end);
        vp.addEventListener('pointercancel', end);
        // Chỉ phóng to khi giữ Ctrl để không chặn việc cuộn trang.
        vp.addEventListener('wheel', function (e) {
            if (!e.ctrlKey) return;
            e.preventDefault();
            var r = vp.getBoundingClientRect();
            zoomAt(e.deltaY < 0 ? 1.1 : 1 / 1.1, e.clientX - r.left, e.clientY - r.top);
        }, { passive: false });
        vp.addEventListener('keydown', function (e) {
            var step = 40, handled = true;
            if (e.key === 'ArrowLeft') state.panX += step;
            else if (e.key === 'ArrowRight') state.panX -= step;
            else if (e.key === 'ArrowUp') state.panY += step;
            else if (e.key === 'ArrowDown') state.panY -= step;
            else if (e.key === '+' || e.key === '=') zoomAt(1.2, vp.clientWidth / 2, vp.clientHeight / 2);
            else if (e.key === '-') zoomAt(1 / 1.2, vp.clientWidth / 2, vp.clientHeight / 2);
            else handled = false;
            if (handled) { e.preventDefault(); applyTransform(); }
        });
        applyTransform();
        if (!state.fitted) { state.fitted = true; fitTo(state.round === 'all' ? null : roundBox()); }
    }

    // ===== Danh sách theo vòng (BRK-11) =====
    function listHtml() {
        var html = '';
        board.stages.forEach(function (stage) {
            if (board.stages.length > 1) html += '<div class="section-title" style="margin:18px 0 0">' + esc(stage.label) + '</div>';
            stage.rounds.forEach(function (round) {
                if (state.round !== 'all' && state.round !== roundKey(stage, round.roundNo)) return;
                var nodes = stage.nodes.filter(function (n) { return n.roundNo === round.roundNo && (state.team === null || hasTeam(n, state.team)); })
                    .sort(function (a, b) { return (a.isThirdPlace ? 1 : 0) - (b.isThirdPlace ? 1 : 0) || a.position - b.position; });
                if (!nodes.length) return;
                html += '<div class="bk-list-round" data-list-round="' + roundKey(stage, round.roundNo) + '">' +
                    '<div class="section-title" style="margin:0 0 10px;font-size:1rem">' + esc(round.label) + '</div>' +
                    nodes.map(function (n) {
                        var right = n.isBye ? '' : (n.statusLabel ? '<span class="badge ' + esc(n.statusClass) + '">' + esc(n.statusLabel) + '</span>' : '');
                        var when = n.scheduledAt && n.status === 'PENDING' ? formatTime(n.scheduledAt) : '';
                        return '<div class="bk-card" data-card="' + n.nodeId + '">' +
                            '<button type="button" class="bk-head" data-detail="' + n.nodeId + '"><span>' + esc(n.label) + (when ? ' · ' + esc(when) : '') + '</span>' + right + '</button>' +
                            teamRow(n, n.teamA, n.isBye ? null : n.scoreA) + teamRow(n, n.teamB, n.isBye ? null : n.scoreB) + '</div>';
                    }).join('') + '</div>';
            });
        });
        return '<div id="bk-list">' + (html || '<div class="hint">Không có trận nào trong vòng đã chọn.</div>') + '</div>';
    }

    // ===== Hành trình đội và chi tiết trận =====
    function journeyHtml() {
        if (state.team === null) return '';
        var items = allNodes().filter(function (x) { return hasTeam(x.node, state.team) && !x.node.isBye; });
        var name = '';
        allNodes().some(function (x) {
            if (x.node.teamA.teamId === state.team) { name = x.node.teamA.name; return true; }
            if (x.node.teamB.teamId === state.team) { name = x.node.teamB.name; return true; }
            return false;
        });
        var rows = items.map(function (x) {
            var n = x.node;
            var isA = n.teamA.teamId === state.team;
            var opp = isA ? n.teamB.name : n.teamA.name;
            var mine = isA ? n.scoreA : n.scoreB, theirs = isA ? n.scoreB : n.scoreA;
            var result = n.winnerTeamId === state.team ? 'Thắng' : n.winnerTeamId ? 'Thua' : (n.scheduledAt ? 'Chưa đấu · ' + formatTime(n.scheduledAt) : 'Chưa đấu');
            var score = n.winnerTeamId && mine !== null && mine !== undefined ? ' ' + mine + '–' + theirs : '';
            return '<li>' + esc(n.label) + ': gặp ' + esc(opp) + ' — <strong>' + esc(result + score) + '</strong></li>';
        }).join('');
        return '<div class="card-etoms" id="journey" style="margin-bottom:12px"><div class="body">' +
            '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2">' +
            '<div class="section-title m-0">Hành trình của ' + esc(name) + '</div>' +
            '<button type="button" class="btn btn-ghost" id="clear-team">Bỏ chọn đội</button></div>' +
            (rows ? '<ol style="margin:10px 0 0;padding-left:22px">' + rows + '</ol>' : '<div class="hint mt-2">Đội chưa có trận nào trong nhánh knockout.</div>') +
            '</div></div>';
    }

    function detailHtml() {
        if (state.detail === null) return '';
        var found = allNodes().find(function (x) { return x.node.nodeId === state.detail; });
        if (!found) return '';
        var n = found.node;
        var games = n.games.length
            ? '<table><thead><tr><th>Ván</th><th>' + esc(n.teamA.name) + '</th><th>' + esc(n.teamB.name) + '</th></tr></thead><tbody>' +
              n.games.map(function (g) { return '<tr><td>' + g.gameNo + '</td><td>' + val(g.scoreA) + '</td><td>' + val(g.scoreB) + '</td></tr>'; }).join('') + '</tbody></table>'
            : '';
        var lines = [];
        if (n.isBye) lines.push(esc(n.teamA.name) + ' được miễn vòng 1 và vào thẳng vòng sau.');
        else {
            lines.push('<strong>' + esc(n.teamA.name) + '</strong> ' + val(n.scoreA) + ' – ' + val(n.scoreB) + ' <strong>' + esc(n.teamB.name) + '</strong>');
            lines.push('Trạng thái: ' + esc(n.statusLabel || '') + (n.bestOf ? ' · BO' + n.bestOf : ''));
            lines.push(n.scheduledAt ? 'Giờ thi đấu: ' + esc(formatTime(n.scheduledAt)) : 'Chưa xếp giờ thi đấu');
            lines.push(n.refereeName ? 'Trọng tài: ' + esc(n.refereeName) : 'Chưa có trọng tài, Ban tổ chức phân xử');
        }
        return '<div class="card-etoms bk-detail" id="match-detail" style="margin-bottom:12px"><div class="body">' +
            '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2">' +
            '<div class="section-title m-0">' + esc(n.label) + '</div>' +
            '<button type="button" class="btn btn-ghost" id="close-detail">Đóng</button></div>' +
            '<div class="mt-2">' + lines.map(function (l) { return '<div>' + l + '</div>'; }).join('') + '</div>' + games +
            (n.matchId ? '<div class="btn-row"><a class="btn btn-ghost" href="/match/pick-ban.html?id=' + n.matchId + '">Cấm chọn bản đồ</a></div>' : '') +
            '</div></div>';
    }

    // ===== Vòng bảng bên cạnh nhánh (UC-20 A2) =====
    function groupsHtml() {
        if (!board.hasGroupStage || !standings || !standings.groups.length) return '';
        var marks = { QUALIFIED: 'Đi tiếp', PROVISIONAL: 'Tạm đi tiếp', PENDING_DECISION: 'Chờ BTC' };
        return '<div class="card-etoms" id="group-summary" style="margin-top:16px"><div class="body">' +
            '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">' +
            '<div class="section-title m-0">Vòng bảng</div>' +
            '<a class="btn btn-ghost" href="/tournament/standings.html?id=' + tournamentId + '">Xem bảng xếp hạng đầy đủ</a></div>' +
            '<div class="hint mb-2">' + esc(standings.stateMessage) + '</div>' +
            '<div class="bk-groups">' + standings.groups.map(function (g) {
                return '<div><div class="nm" style="margin-bottom:4px">Bảng ' + esc(g.groupLabel) + '</div><table class="bk-mini"><tbody>' +
                    g.rows.map(function (r) {
                        return '<tr><td>' + r.position + '</td><td>' + esc(r.teamName) + (marks[r.mark] ? ' <span class="badge badge-active">' + marks[r.mark] + '</span>' : '') +
                            '</td><td class="text-end"><strong>' + r.points + '</strong></td></tr>';
                    }).join('') + '</tbody></table></div>';
            }).join('') + '</div></div></div>';
    }

    function toolbarHtml() {
        var options = '<option value="all">Tất cả vòng</option>' + board.stages.map(function (s) {
            return s.rounds.map(function (r) {
                var key = roundKey(s, r.roundNo);
                return '<option value="' + key + '"' + (state.round === key ? ' selected' : '') + '>' + esc((board.stages.length > 1 ? s.label + ' · ' : '') + r.label) + '</option>';
            }).join('');
        }).join('');
        var tree = state.mode === 'tree';
        return '<div class="bk-toolbar">' +
            '<div class="bk-seg" role="group" aria-label="Cách xem">' +
            '<button type="button" data-mode="tree" aria-pressed="' + tree + '">Sơ đồ</button>' +
            '<button type="button" data-mode="list" aria-pressed="' + !tree + '">Danh sách</button></div>' +
            '<select class="select filter-select" id="round-filter" aria-label="Lọc theo vòng" style="max-width:240px">' + options + '</select>' +
            '<span class="grow"></span>' +
            (tree ? '<div class="d-flex gap-2 align-items-center">' +
                '<button type="button" class="btn btn-ghost" id="zoom-out" aria-label="Thu nhỏ">−</button>' +
                '<span id="zoom-label" class="rl" style="min-width:44px;text-align:center">100%</span>' +
                '<button type="button" class="btn btn-ghost" id="zoom-in" aria-label="Phóng to">+</button>' +
                '<button type="button" class="btn btn-ghost" id="zoom-fit">Vừa khung</button></div>' : '') +
            '</div>';
    }

    function render() {
        var b = board;
        document.getElementById('page-title').textContent = 'Nhánh đấu · ' + b.tournamentName;
        document.getElementById('page-sub').textContent = b.formatLabel + ' · ' + b.tournamentStatus +
            (b.isLive ? ' · Tự cập nhật mỗi 30 giây' : '');

        var notice = b.notice ? '<div class="pending-banner mb-2">' + esc(b.notice) + '</div>' : '';
        var updated = '<div class="hint" id="updated-at" style="margin-bottom:10px">Cập nhật lúc ' + new Date().toLocaleTimeString('vi-VN') + '</div>';

        if (!b.canViewBracket) {
            document.getElementById('bracket-root').innerHTML = '<div class="card-etoms"><div class="body">' + notice +
                (b.participants.length
                    ? '<ol id="participants" style="margin:0;padding-left:22px">' + b.participants.map(function (p) { return '<li>' + esc(p) + '</li>'; }).join('') + '</ol>'
                    : '<div class="hint">Chưa có đội nào được duyệt.</div>') + '</div></div>';
            return;
        }
        if (!b.stages.length) {
            document.getElementById('bracket-root').innerHTML = notice + updated +
                (b.hasGroupStage ? '<div class="card-etoms"><div class="body"><div class="hint">Giải đang ở vòng bảng, nhánh knockout sẽ có sau khi vòng bảng kết thúc.</div></div></div>'
                    : '<div class="card-etoms"><div class="empty"><h3>Giải chưa có nhánh đấu</h3><p>Quay lại khi Ban tổ chức đã sinh nhánh.</p></div></div>') +
                groupsHtml();
            return;
        }

        document.getElementById('bracket-root').innerHTML = notice + updated + toolbarHtml() + journeyHtml() + detailHtml() +
            (state.mode === 'tree' ? treeHtml() : listHtml()) + groupsHtml();
        wire();
    }

    function wire() {
        document.querySelectorAll('[data-mode]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                state.userMode = btn.dataset.mode;
                setMode(btn.dataset.mode);
            });
        });
        document.getElementById('round-filter').addEventListener('change', function (e) {
            state.round = e.target.value;
            render();
            if (state.mode === 'tree') fitTo(state.round === 'all' ? null : roundBox());
        });
        document.querySelectorAll('[data-team]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var id = parseInt(btn.dataset.team, 10);
                state.team = state.team === id ? null : id;
                render();
            });
        });
        document.querySelectorAll('[data-detail]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var id = parseInt(btn.dataset.detail, 10);
                state.detail = state.detail === id ? null : id;
                render();
                var panel = document.getElementById('match-detail');
                if (panel) panel.scrollIntoView({ block: 'nearest' });
            });
        });
        var clearTeam = document.getElementById('clear-team');
        if (clearTeam) clearTeam.addEventListener('click', function () { state.team = null; render(); });
        var closeDetail = document.getElementById('close-detail');
        if (closeDetail) closeDetail.addEventListener('click', function () { state.detail = null; render(); });
        if (state.mode === 'tree') {
            document.getElementById('zoom-in').addEventListener('click', function () { var vp = document.getElementById('bk-viewport'); zoomAt(1.2, vp.clientWidth / 2, vp.clientHeight / 2); });
            document.getElementById('zoom-out').addEventListener('click', function () { var vp = document.getElementById('bk-viewport'); zoomAt(1 / 1.2, vp.clientWidth / 2, vp.clientHeight / 2); });
            document.getElementById('zoom-fit').addEventListener('click', function () { fitTo(null); });
            wireViewport();
        }
    }

    function setMode(mode) {
        if (state.mode === mode) return;
        state.mode = mode;
        state.fitted = false;
        if (board) render();
    }

    async function load(first) {
        try {
            var res = await Api.get('/api/tournaments/' + tournamentId + '/bracket-view');
            var changed = !board || board.version !== res.data.version;
            board = res.data;
            if (board.hasGroupStage) {
                try { standings = (await Api.get('/api/tournaments/' + tournamentId + '/standings')).data; } catch (e) { standings = null; }
            }
            if (changed || first || board.hasGroupStage) render();
            else {
                var u = document.getElementById('updated-at');
                if (u) u.textContent = 'Cập nhật lúc ' + new Date().toLocaleTimeString('vi-VN');
            }
        } catch (err) {
            if (!first) { scheduleRefresh(); return; }
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại hoặc chưa được công bố.');
            else showEmpty('Không tải được nhánh đấu', err.message);
            return;
        }
        scheduleRefresh();
    }

    // Giải đang diễn ra thì tự làm mới, chỉ khi trang đang được xem.
    function scheduleRefresh() {
        clearTimeout(timer);
        if (!board || !board.isLive) return;
        timer = setTimeout(function () {
            if (document.visibilityState === 'visible') load(false);
            else scheduleRefresh();
        }, REFRESH_MS);
    }

    document.addEventListener('DOMContentLoaded', function () {
        tournamentId = Ui.qsInt('id');
        if (tournamentId === null) {
            showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.');
            return;
        }
        // BRK-11: màn hình hẹp tự chuyển sang danh sách; người xem vẫn chọn lại được.
        state.mode = MOBILE.matches ? 'list' : 'tree';
        var onChange = function () { if (!state.userMode) setMode(MOBILE.matches ? 'list' : 'tree'); };
        if (MOBILE.addEventListener) MOBILE.addEventListener('change', onChange); else MOBILE.addListener(onChange);
        load(true);
    });
})();
