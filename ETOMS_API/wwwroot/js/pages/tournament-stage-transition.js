(function () {
    var STATE_BADGES = {
        NOT_HYBRID: ['badge-danger', 'Không áp dụng'],
        NO_GROUP_STAGE: ['badge-danger', 'Chưa có vòng bảng'],
        NOT_LOCKED: ['badge-gold', 'Chưa khóa nhánh'],
        IN_PROGRESS: ['badge-gold', 'Vòng bảng đang đấu'],
        NEEDS_DECISION: ['badge-danger', 'Cần Ban tổ chức xếp hạng'],
        NO_KNOCKOUT: ['badge-gold', 'Không cần knockout'],
        READY: ['badge-active', 'Sẵn sàng tạo knockout'],
        DONE: ['badge-active', 'Đã tạo knockout']
    };

    var tournamentId = null;
    var undecided = null;   // bảng lúc chưa áp thứ tự đội hòa
    var orders = {};        // tieKey -> danh sách teamId theo thứ tự Ban tổ chức chọn
    var previewing = false;

    function flash() { return document.getElementById('flash'); }

    function showEmpty(title, message) {
        document.getElementById('transition-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    function blockingTies(board) {
        var list = [];
        board.groups.forEach(function (g) {
            g.ties.forEach(function (t) { if (t.blocking) list.push({ group: g.groupLabel, tie: t }); });
        });
        return list;
    }

    function tableHtml(group, board) {
        var qualifiedText = board.state === 'IN_PROGRESS' ? 'Tạm đi tiếp' : 'Đi tiếp';
        var rows = group.rows.map(function (r) {
            var mark = r.qualified ? ' <span class="badge badge-active">' + qualifiedText + '</span>'
                : (r.tieKey ? ' <span class="badge badge-gold">Hòa</span>' : '');
            return '<tr class="' + (r.qualified ? 'qualified' : '') + '">' +
                '<td>' + r.position + '</td>' +
                '<td class="team">' + Ui.escapeHtml(r.teamName) + mark + '</td>' +
                '<td>' + r.played + '</td><td>' + r.won + '</td><td>' + r.lost + '</td>' +
                '<td>' + r.gamesWon + '–' + r.gamesLost + '</td>' +
                '<td>' + (r.diff > 0 ? '+' : '') + r.diff + '</td>' +
                '<td><strong>' + r.points + '</strong></td></tr>';
        }).join('');
        return '<div class="card-etoms" style="margin-top:16px"><div class="body">' +
            '<div class="section-title" style="margin-top:0">Bảng ' + Ui.escapeHtml(group.groupLabel) + '</div>' +
            '<div class="standing-wrap"><table class="standing" data-group="' + Ui.escapeHtml(group.groupLabel) + '"><thead><tr>' +
            '<th>#</th><th class="team">Đội</th><th>Trận</th><th>Thắng</th><th>Thua</th><th>Ván</th><th>Hiệu số</th><th>Điểm</th>' +
            '</tr></thead><tbody>' + rows + '</tbody></table></div></div></div>';
    }

    function tieEditorHtml() {
        var ties = blockingTies(undecided);
        if (!ties.length) return '';
        return '<div class="card-etoms" style="margin-top:16px"><div class="body">' +
            '<div class="section-title" style="margin-top:0">Xếp thứ tự các đội hòa</div>' +
            '<p class="hint" style="margin:6px 0 14px">Các đội dưới đây vẫn bằng nhau sau mọi tiêu chí phân định đã công bố. Hệ thống không tự bốc thăm; Ban tổ chức xếp thứ tự, đội đứng trên xếp hạng cao hơn.</p>' +
            ties.map(function (x) {
                var ids = orders[x.tie.tieKey];
                var names = {};
                x.tie.teamIds.forEach(function (id, i) { names[id] = x.tie.teamNames[i]; });
                return '<div style="margin-bottom:14px"><div class="nm">Bảng ' + Ui.escapeHtml(x.group) + ' · ' + Ui.escapeHtml(x.tie.positionsLabel) + '</div>' +
                    '<ol class="tie-order" data-tie="' + Ui.escapeHtml(x.tie.tieKey) + '" style="margin:6px 0 0;padding-left:22px">' +
                    ids.map(function (id, i) {
                        return '<li><span>' + Ui.escapeHtml(names[id]) + '</span><span class="d-flex gap-2">' +
                            '<button type="button" class="btn btn-ghost" data-tie-move="up" data-tie-key="' + Ui.escapeHtml(x.tie.tieKey) + '" data-index="' + i + '"' + (i === 0 ? ' disabled' : '') + ' aria-label="Đưa ' + Ui.escapeHtml(names[id]) + ' lên">↑</button>' +
                            '<button type="button" class="btn btn-ghost" data-tie-move="down" data-tie-key="' + Ui.escapeHtml(x.tie.tieKey) + '" data-index="' + i + '"' + (i === ids.length - 1 ? ' disabled' : '') + ' aria-label="Đưa ' + Ui.escapeHtml(names[id]) + ' xuống">↓</button>' +
                            '</span></li>';
                    }).join('') + '</ol></div>';
            }).join('') +
            '<div class="btn-row"><button type="button" class="btn btn-ghost" id="preview-btn">Xem trước với thứ tự này</button></div>' +
            '</div></div>';
    }

    function pairingsHtml(board) {
        if (!board.pairings.length) return '';
        var title = board.state === 'DONE' ? 'Vòng 1 knockout đã tạo' : 'Vòng 1 knockout dự kiến';
        return '<div class="card-etoms" style="margin-top:16px"><div class="body">' +
            '<div class="section-title" style="margin-top:0">' + title + '</div>' +
            '<div id="pairing-list">' + board.pairings.map(function (p) {
                var a = Ui.escapeHtml(p.teamAName) + (p.teamALabel ? ' <span class="rl">(' + Ui.escapeHtml(p.teamALabel) + ')</span>' : '');
                var b = p.isBye ? '<span class="rl">được miễn vòng 1</span>'
                    : Ui.escapeHtml(p.teamBName) + (p.teamBLabel ? ' <span class="rl">(' + Ui.escapeHtml(p.teamBLabel) + ')</span>' : '');
                return '<div class="member-card" style="margin-bottom:8px;flex-wrap:wrap;gap:8px" data-pair="' + p.positionInRound + '">' +
                    '<div class="nm" style="min-width:64px">Trận ' + p.positionInRound + '</div>' +
                    '<div class="grow">' + a + (p.isBye ? ' · ' : ' <strong>vs</strong> ') + b + '</div></div>';
            }).join('') + '</div></div></div>';
    }

    function decisionsBody() {
        var list = [];
        blockingTies(undecided).forEach(function (x) {
            list.push({ groupLabel: x.group, teamIds: orders[x.tie.tieKey] });
        });
        return { decisions: list };
    }

    function render(board) {
        document.getElementById('page-title').textContent = 'Chuyển sang knockout · ' + board.tournamentName;
        var config = board.groupCount ? board.groupCount + ' bảng, ' + board.advancePerGroup + ' đội đi tiếp mỗi bảng · ' : '';
        document.getElementById('page-sub').textContent = config + 'Tiêu chí phân định: ' + board.tieBreakerOrder.join(' > ');

        var badge = STATE_BADGES[board.state] || ['badge-gold', board.state];
        var progress = board.totalMatches ? '<div class="hint" style="margin-top:8px">' + board.finishedMatches + '/' + board.totalMatches + ' trận vòng bảng đã có kết quả.</div>' : '';
        var autoNote = board.state === 'READY' && !previewing
            ? '<div class="hint" style="margin-top:8px">Hệ thống cũng tự tạo trong vòng vài phút nếu Ban tổ chức không bấm.</div>' : '';
        var action = board.canAdvance
            ? '<div class="btn-row"><button type="button" class="btn btn-primary" id="advance-btn">Tạo nhánh knockout</button></div>' : '';
        var links = '<div class="btn-row">' +
            '<a class="btn btn-ghost" href="/tournament/manage.html?id=' + tournamentId + '">Quản lý giải</a>' +
            (board.state === 'DONE' ? '<a class="btn btn-ghost" href="/tournament/referees.html?id=' + tournamentId + '">Gán trọng tài</a>' : '') +
            '</div>';

        document.getElementById('transition-root').innerHTML =
            '<div class="card-etoms"><div class="body">' +
            '<div>Trạng thái: <span class="badge ' + badge[0] + '" id="state-badge">' + badge[1] + '</span></div>' +
            '<p style="margin:10px 0 0" id="state-message">' + Ui.escapeHtml(previewing ? 'Đang xem trước với thứ tự đội hòa bạn chọn. Chưa có gì được lưu.' : board.stateMessage) + '</p>' +
            progress + autoNote + action + '</div></div>' +
            (undecided && undecided.state === 'NEEDS_DECISION' ? tieEditorHtml() : '') +
            pairingsHtml(board) +
            board.groups.map(function (g) { return tableHtml(g, board); }).join('') +
            '<div style="margin-top:16px">' + links + '</div>';

        wire();
    }

    function wire() {
        document.querySelectorAll('[data-tie-move]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var ids = orders[btn.dataset.tieKey];
                var i = parseInt(btn.dataset.index, 10);
                var j = btn.dataset.tieMove === 'up' ? i - 1 : i + 1;
                var tmp = ids[i]; ids[i] = ids[j]; ids[j] = tmp;
                previewing = false;
                render(undecided);
            });
        });
        var preview = document.getElementById('preview-btn');
        if (preview) preview.addEventListener('click', async function () {
            Ui.clearAlert(flash());
            try {
                var res = await Api.post('/api/tournaments/' + tournamentId + '/stage-transition/preview', decisionsBody());
                previewing = true;
                render(res.data);
            } catch (err) {
                Ui.showAlert(flash(), 'danger', err.message);
            }
        });
        var advance = document.getElementById('advance-btn');
        if (advance) advance.addEventListener('click', advanceStage);
    }

    async function advanceStage() {
        var ok = window.etomsConfirm
            ? await window.etomsConfirm('Tạo nhánh knockout từ kết quả vòng bảng? Sau khi tạo, thứ hạng vòng bảng và các cặp đấu vòng 1 không đổi được nữa.')
            : true;
        if (!ok) return;
        Ui.clearAlert(flash());
        var btn = document.getElementById('advance-btn');
        btn.disabled = true;
        try {
            var res = await Api.post('/api/tournaments/' + tournamentId + '/stage-transition', decisionsBody());
            previewing = false;
            load(res.data);
            Ui.showAlert(flash(), 'success', res.message);
        } catch (err) {
            Ui.showAlert(flash(), 'danger', err.message);
            btn.disabled = false;
        }
    }

    function load(board) {
        undecided = board;
        orders = {};
        blockingTies(board).forEach(function (x) { orders[x.tie.tieKey] = x.tie.teamIds.slice(); });
        render(board);
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        tournamentId = Ui.qsInt('id');
        if (tournamentId === null) {
            showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.');
            return;
        }
        try {
            var res = await Api.get('/api/tournaments/' + tournamentId + '/stage-transition');
            load(res.data);
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể chuyển giai đoạn cho giải này', 'Chỉ Ban tổ chức của giải được tạo nhánh knockout.');
            else showEmpty('Không tải được vòng bảng', err.message);
        }
    });
})();
