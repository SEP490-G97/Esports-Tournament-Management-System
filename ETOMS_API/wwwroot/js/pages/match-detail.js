// STUB TẠM thay module MAT (Vũ Đức Hải: MAT-01/04/05/06/07, Nguyễn Quang Anh: MAT-08..12) —
// chỉ đủ để demo luồng vào trận, không phải bài làm cá nhân của hai bạn.
(function () {
    var matchId = null;
    var me = null;
    var root, flash;

    var STATUS_LABEL = {
        PENDING: 'Chờ điểm danh', READY: 'Sẵn sàng', LIVE: 'Đang diễn ra',
        WAITING_REPORT: 'Chờ báo cáo tỉ số', DISPUTED: 'Đang tranh chấp',
        VERIFIED: 'Đã xác nhận', WALKOVER: 'Thắng trắng / Xử thua', CANCELLED: 'Đã hủy'
    };

    function statusLabel(s) { return STATUS_LABEL[s] || s; }

    async function load() {
        var detail;
        try { detail = await Api.get('/api/matches/' + matchId + '/ops'); }
        catch (e) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy trận đấu</h3></div></div>'; return; }
        render(detail);
    }

    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    function fmtFull(iso) { var t = utc(iso); return t ? t.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' }) : ''; }

    // Phần đầu trang: bảng tỉ số hai đội (logo, tên, điểm danh), tỉ số chính thức hoặc giờ thi đấu ở giữa.
    function heroHtml(d) {
        var done = d.status === 'VERIFIED' || d.status === 'WALKOVER';
        function team(side) {
            var id = side === 'A' ? d.teamAId : d.teamBId;
            var name = side === 'A' ? d.teamAName : d.teamBName;
            var logo = Ui.safeUrl(side === 'A' ? d.teamALogoUrl : d.teamBLogoUrl);
            var tag = side === 'A' ? d.teamATag : d.teamBTag;
            var checked = side === 'A' ? d.teamACheckedIn : d.teamBCheckedIn;
            var win = d.winnerTeamId && d.winnerTeamId === id, lose = d.winnerTeamId && id && d.winnerTeamId !== id;
            var avatar = logo ? '<img src="' + esc(logo) + '" alt="" />' : '<span>' + esc((name || '?').trim().slice(0, 1).toUpperCase()) + '</span>';
            var pill = !id ? '<span class="mh-pill">Chờ đội thắng trận trước</span>'
                : d.doubleLoss ? '<span class="mh-pill">Xử thua</span>'
                : done || d.status === 'CANCELLED' ? (win ? '<span class="mh-pill win">Thắng</span>' : '')
                : d.status !== 'PENDING' && d.status !== 'READY' ? ''
                : checked ? '<span class="mh-pill ok">✓ Đã điểm danh</span>' : '<span class="mh-pill">Chưa điểm danh</span>';
            var inner = '<div class="mh-logo">' + avatar + '</div><div class="mh-name">' + esc(name) + '</div>' +
                (tag ? '<div class="mh-tag">' + esc(tag) + '</div>' : '') + pill;
            return id ? '<a class="mh-team' + (win ? ' is-win' : '') + (lose ? ' is-lose' : '') + '" href="/team/details.html?id=' + Number(id) + '">' + inner + '</a>'
                : '<div class="mh-team is-tbd">' + inner + '</div>';
        }
        var center = done
            ? '<div class="mh-score"><span class="' + (d.winnerTeamId === d.teamAId ? 'w' : '') + '">' + (d.scoreA ?? '–') + '</span><i>:</i><span class="' + (d.winnerTeamId === d.teamBId ? 'w' : '') + '">' + (d.scoreB ?? '–') + '</span></div>' +
              '<div class="mh-state">' + (d.status === 'WALKOVER' ? 'Thắng trắng' : 'Kết thúc') + '</div>'
            : '<div class="mh-vs">VS</div><div class="mh-state' + (d.status === 'LIVE' ? ' live' : '') + '">' +
              esc(!d.scheduledAt && d.status === 'PENDING' ? 'Chưa xếp lịch' : statusLabel(d.status)) + '</div>' +
              (d.scheduledAt ? '<div class="mh-time">' + esc(fmtFull(d.scheduledAt)) + '</div>' : '');
        var meta = ['BO' + Number(d.bestOf)];
        if (d.gameTitle) meta.unshift(esc(d.gameTitle));
        if (d.refereeName) meta.push('Trọng tài: ' + esc(d.refereeName));
        return '<section class="match-hero">' +
            '<a class="mh-tournament" href="/bracket/view.html?id=' + Number(d.tournamentId) + '">← ' + esc(d.tournamentName || 'Về giải đấu') + '</a>' +
            '<h1 class="visually-hidden">' + esc(d.teamAName) + ' đấu với ' + esc(d.teamBName) + '</h1>' +
            '<div class="mh-board">' + team('A') + '<div class="mh-center">' + center + '</div>' + team('B') + '</div>' +
            '<div class="mh-meta">' + meta.map(function (m) { return '<span>' + m + '</span>'; }).join('') + '</div>' +
            stepsHtml(d) + '</section>';
    }

    // Tiến trình trận: Lên lịch → Điểm danh → Thi đấu → Xác nhận kết quả.
    function stepsHtml(d) {
        if (d.status === 'CANCELLED') return '<div class="mh-cancelled">' + (d.awaitingDecision ? 'Kết quả bị hủy sau tranh chấp — chờ Ban tổ chức xử lý.'
            : d.doubleLoss ? 'Trận đã bị hủy — cả hai đội bị xử thua.' : 'Trận đã bị hủy.') + '</div>';
        var both = d.teamAId && d.teamBId;
        var stage = !both ? 0
            : d.status === 'PENDING' ? (d.scheduledAt ? 1 : 0)
            : d.status === 'READY' ? 2
            : d.status === 'LIVE' ? 2
            : d.status === 'WAITING_REPORT' || d.status === 'DISPUTED' ? 3 : 4;
        var steps = ['Xếp lịch', 'Điểm danh', 'Thi đấu', 'Xác nhận kết quả'];
        return '<ol class="mh-steps">' + steps.map(function (s, i) {
            var cls = i < stage ? 'done' : i === stage ? (d.status === 'DISPUTED' && i === 3 ? 'warn' : 'now') : '';
            return '<li class="' + cls + '"><span class="dot"></span>' + s + '</li>';
        }).join('') + '</ol>';
    }

    // Khối kết quả viết cho người dùng cuối: mỗi vai trò chỉ thấy điều cần biết và nút việc của mình.
    // (Tỉ số chính thức chỉ vào qua 2 báo cáo của 2 đội trưởng hoặc phán quyết tranh chấp — LI-01.)
    function resultCardHtml(d) {
        var done = d.status === 'VERIFIED' || d.status === 'WALKOVER';
        var open = ['PENDING', 'READY', 'LIVE', 'WAITING_REPORT'].indexOf(d.status) >= 0;
        var bothTeams = d.teamAId && d.teamBId;
        var mine = d.myCaptainTeamId, myReported = mine && (mine === d.teamAId ? d.teamAReported : d.teamBReported);
        var icon = '', title = '', text = '', actions = '';
        if (done) {
            var winner = d.winnerTeamId === d.teamAId ? d.teamAName : d.teamBName;
            icon = '🏆'; title = winner + ' thắng' + (d.status === 'WALKOVER' ? ' (thắng trắng)' : ' ' + (d.scoreA ?? '–') + ' – ' + (d.scoreB ?? '–'));
            text = (d.hasRuling ? 'Kết quả theo phán quyết của trọng tài sau tranh chấp. ' : 'Kết quả đã được xác nhận. ') + esc(winner) + ' đi tiếp vào trận sau của nhánh đấu.';
            actions = (d.hasRuling ? '<a class="btn btn-primary" href="/result/history.html?matchId=' + d.matchId + '">Xem phán quyết</a>' : '') +
                '<a class="btn btn-ghost" href="/bracket/view.html?id=' + Number(d.tournamentId) + '">Xem nhánh đấu</a>';
        } else if (d.status === 'CANCELLED' && d.awaitingDecision) {
            // Kết quả bị hủy sau phán quyết tranh chấp: nhánh dừng ở trận này cho tới khi BTC xử thắng / tước quyền / cho đấu lại.
            icon = '⚖️'; title = 'Kết quả trận đã bị hủy sau tranh chấp';
            text = d.canManage ? 'Nhánh đấu đang dừng ở trận này. Hãy xử thắng trắng cho đội không vi phạm, tước quyền thi đấu của đội vi phạm, hủy trận (xử thua cả hai đội) hoặc cho hai đội đấu lại — dùng các nút ở mục Công cụ trọng tài & Ban tổ chức bên dưới.'
                : 'Ban tổ chức đang xử lý kết quả trận này. Đội đi tiếp sẽ được cập nhật vào nhánh đấu ngay khi có quyết định.';
            actions = '<a class="btn btn-ghost" href="/result/history.html?matchId=' + d.matchId + '">Xem phán quyết</a>';
        } else if (d.doubleLoss) {
            icon = '⛔'; title = 'Trận đã hủy — cả hai đội bị xử thua';
            text = 'Không đội nào đi tiếp từ trận này. Ở vòng loại trực tiếp, đối thủ của trận sau được xử thắng trắng; ở vòng bảng, mỗi đội tính một trận thua.';
            actions = '<a class="btn btn-ghost" href="/bracket/view.html?id=' + Number(d.tournamentId) + '">Xem nhánh đấu</a>';
        } else if (d.status === 'CANCELLED') {
            icon = '⛔'; title = 'Trận đã hủy'; text = 'Trận này không diễn ra. Xem nhánh đấu để biết lịch các trận còn lại.';
        } else if (!bothTeams) {
            icon = '⏳'; title = 'Đang chờ xác định đối thủ'; text = 'Đội thắng ở trận trước sẽ tự vào trận này khi có kết quả.';
        } else if (d.openDisputeId) {
            icon = '⚖️'; title = 'Kết quả đang được xem xét';
            text = d.canManage ? 'Báo cáo tỉ số của hai đội không khớp. Hãy xem bằng chứng và ra phán quyết; đội thắng sẽ tự vào vòng sau.'
                : 'Hai đội báo cáo tỉ số chưa khớp nhau, trọng tài đang xem bằng chứng. Kết quả sẽ cập nhật ngay khi có phán quyết.';
            actions = (d.canManage ? '<a class="btn btn-primary" href="/result/dispute.html?id=' + d.openDisputeId + '">Xử lý tranh chấp</a>' : '') +
                (mine ? '<a class="btn btn-ghost" href="/result/evidence.html?matchId=' + d.matchId + '">Bổ sung bằng chứng</a>' : '') +
                '<a class="btn btn-ghost" href="/result/history.html?matchId=' + d.matchId + '">Xem diễn biến tranh chấp</a>';
        } else if (open) {
            var deadline = d.reportDeadlineAt ? fmtFull(d.reportDeadlineAt) : '';
            if (mine) {
                icon = myReported ? '✅' : '📝';
                title = myReported ? 'Đội bạn đã báo cáo tỉ số' : 'Báo cáo tỉ số sau khi đấu xong';
                text = myReported ? 'Đang chờ đội bạn kia báo cáo. Hai bên khớp nhau là kết quả được xác nhận ngay.'
                    : 'Đấu xong, đội trưởng gửi tỉ số kèm ảnh kết quả. Đội bạn kia không xem được tỉ số bạn khai; hai bên khớp nhau là kết quả được xác nhận ngay.';
                if (deadline) text += ' Hạn báo cáo: ' + deadline + '.';
                actions = '<a class="btn btn-primary" href="/result/submit.html?matchId=' + d.matchId + '">' + (myReported ? 'Xem báo cáo đã gửi' : 'Báo cáo tỉ số') + '</a>';
            } else if (d.canManage) {
                icon = '📋'; title = 'Chờ hai đội báo cáo tỉ số';
                text = 'Kết quả tự xác nhận khi báo cáo của hai đội khớp nhau; không khớp hoặc quá hạn thì chuyển thành tranh chấp để bạn phán quyết.' + (deadline ? ' Hạn báo cáo: ' + deadline + '.' : '');
                actions = '<a class="btn btn-ghost" href="/result/submit.html?matchId=' + d.matchId + '">Xem tình trạng báo cáo</a>';
            } else {
                icon = '🎮'; title = d.status === 'LIVE' ? 'Trận đang diễn ra' : 'Trận chưa có kết quả';
                text = 'Kết quả chính thức sẽ hiện ở đây sau khi trận kết thúc và hai đội xác nhận tỉ số.';
            }
        }
        var reports = (open && bothTeams && !d.openDisputeId && (mine || d.canManage))
            ? '<div class="mr-reports">' + [[d.teamAName, d.teamAReported], [d.teamBName, d.teamBReported]].map(function (x) {
                return '<span class="mr-report' + (x[1] ? ' ok' : '') + '">' + esc(x[0]) + ': ' + (x[1] ? 'đã báo cáo' : 'chưa báo cáo') + '</span>';
            }).join('') + '</div>' : '';
        return '<section class="match-result card-etoms' + (done ? ' is-done' : d.openDisputeId ? ' is-dispute' : '') + '"><div class="body">' +
            '<div class="mr-icon" aria-hidden="true">' + icon + '</div><div class="mr-body"><div class="mr-label">Kết quả trận</div>' +
            '<h2>' + esc(title) + '</h2><p>' + esc(text) + '</p>' + reports +
            (actions ? '<div class="btn-row mt-2">' + actions + '</div>' : '') + '</div></div></section>';
    }

    function render(d) {
        // Trận đã xong / đã hủy thì ẩn các thao tác chỉ dùng khi trận còn diễn ra (thay người, bỏ cuộc, xử thắng trắng...).
        var bothTeams = !!(d.teamAId && d.teamBId);
        // Chưa đủ hai đội (chờ đội thắng trận trước) thì chưa có gì để thay người / bỏ cuộc / nhập phòng.
        var playable = bothTeams && ['PENDING', 'READY', 'LIVE'].indexOf(d.status) >= 0;
        var closed = ['VERIFIED', 'WALKOVER', 'CANCELLED'].indexOf(d.status) >= 0;
        document.title = d.teamAName + ' vs ' + d.teamBName + ' · ETOMS';
        var html = '<div class="page-head">' + heroHtml(d) + '</div>';

        html += resultCardHtml(d);

        // Điểm danh: trạng thái từng đội đã ở bảng tỉ số; khối này chỉ cho đội trưởng / trọng tài khi còn điểm danh được.
        var checkIn = checkInWindowHtml(d);
        var manual = d.canManage && playable && (!d.teamACheckedIn || !d.teamBCheckedIn)
            ? '<div class="btn-row">' +
              (d.teamACheckedIn ? '' : '<button class="btn btn-ghost" data-act="checkin" data-side="A">Điểm danh hộ ' + esc(d.teamAName) + '</button>') +
              (d.teamBCheckedIn ? '' : '<button class="btn btn-ghost" data-act="checkin" data-side="B">Điểm danh hộ ' + esc(d.teamBName) + '</button>') + '</div>'
            : '';
        if ((d.myCaptainTeamId || d.canManage) && (checkIn || manual)) {
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Điểm danh</h3>' + checkIn + manual + '</div></div>';
        }

        if ((d.canManage || d.myCaptainTeamId) && bothTeams && !(closed && !d.roomCode)) {
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Phòng đấu</h3>';
            if (d.roomCode) {
                // Đội trưởng chỉ xem và sao chép; chỉ trọng tài / Ban tổ chức sửa được (form bên dưới).
                html += '<div class="room-info">' +
                    '<div class="room-field"><span class="room-label">Mã phòng</span><b class="room-value">' + esc(d.roomCode) + '</b>' +
                    '<button type="button" class="btn btn-ghost btn-sm" data-copy="' + esc(d.roomCode) + '">Sao chép</button></div>' +
                    (d.roomPassword ? '<div class="room-field"><span class="room-label">Mật khẩu</span><b class="room-value">' + esc(d.roomPassword) + '</b>' +
                        '<button type="button" class="btn btn-ghost btn-sm" data-copy="' + esc(d.roomPassword) + '">Sao chép</button></div>' : '') +
                    '</div>';
            } else {
                html += '<p class="muted">' + (d.canSetRoom ? 'Chưa có thông tin phòng. Nhập mã phòng để hai đội vào đấu.' : 'Chưa có thông tin phòng. Trọng tài hoặc Ban tổ chức sẽ cập nhật mã phòng trước giờ thi đấu.') + '</p>';
            }
            if (d.canSetRoom) {
                // Chỉ trọng tài / Ban tổ chức nhập và sửa mã phòng; hai đội trưởng xem và sao chép.
                html += '<form id="roomForm" class="btn-row" style="align-items:flex-start" novalidate>' +
                    '<div><label>Mã phòng</label><input class="input" name="roomCode" maxlength="100" value="' + Ui.escapeHtml(d.roomCode || '') + '">' +
                    '<span class="field-error" data-field-error="RoomCode"></span></div>' +
                    '<div><label>Mật khẩu (tuỳ chọn)</label><input class="input" name="roomPassword" maxlength="100" value="' + Ui.escapeHtml(d.roomPassword || '') + '">' +
                    '<span class="field-error" data-field-error="RoomPassword"></span></div>' +
                    '<button class="btn btn-primary" type="submit" style="margin-top:26px">Lưu</button></form>';
            }
            html += '</div></div>';
        }

        if (d.myCaptainTeamId && playable) {
            // Chọn theo tên thành viên đội mình (không bắt đội trưởng nhớ mã tài khoản).
            var members = d.myTeamMembers || [];
            var slotLabel = function (s) { return s === 'STARTER' ? 'chính thức' : s === 'SUBSTITUTE' ? 'dự bị' : s; };
            var opts = function (preferSlot) {
                return '<option value="">— Chọn thành viên —</option>' + members.map(function (m) {
                    return '<option value="' + m.accountId + '"' + (m.slot === preferSlot ? '' : ' class="text-muted"') + '>' +
                        Ui.escapeHtml(m.fullName) + ' (' + slotLabel(m.slot) + ')</option>';
                }).join('');
            };
            var hasSub = members.some(function (m) { return m.slot === 'SUBSTITUTE'; });
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Xin thay người</h3>' +
                '<p class="muted">Trọng tài hoặc Ban tổ chức sẽ duyệt yêu cầu trước khi người vào được thi đấu.</p>' +
                (hasSub ? '' : '<p class="field-hint">Đội chưa có thành viên dự bị; vẫn có thể đổi giữa các thành viên chính thức.</p>') +
                '<form id="subForm" class="btn-row" style="align-items:flex-end" novalidate>' +
                '<div><label>Người ra</label><select class="input" name="outAccountId" required>' + opts('STARTER') + '</select>' +
                '<span class="field-error" data-field-error="OutAccountId"></span></div>' +
                '<div><label>Người vào</label><select class="input" name="inAccountId" required>' + opts('SUBSTITUTE') + '</select>' +
                '<span class="field-error" data-field-error="InAccountId"></span></div>' +
                '<div style="flex:1 1 240px"><label>Lý do</label><input class="input" name="reason" minlength="5" maxlength="500" required placeholder="Ví dụ: tuyển thủ bị ốm">' +
                '<span class="field-error" data-field-error="Reason"></span></div>' +
                '<button class="btn btn-primary" type="submit">Gửi yêu cầu</button></form></div></div>';
        }

        if (d.canManage) {
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body" id="subList"><h3>Yêu cầu thay người</h3><p class="muted">Đang tải...</p></div></div>';
        }

        if ((d.canManage || d.myCaptainTeamId) && bothTeams) {
            // Phòng chờ (MAT-07 / UC-25) do match-lobby.js vẽ và tự làm mới.
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body" id="lobby-box"></div></div>';
        }

        if (d.canManage) {
            html += '<div class="card-etoms"><div class="body"><h3>Công cụ trọng tài &amp; Ban tổ chức</h3><div class="btn-row">' +
            ((closed && !d.awaitingDecision) || !bothTeams ? '' :
                '<button class="btn btn-ghost" data-act="walkover" data-winner="' + d.teamAId + '">Thắng trắng cho ' + Ui.escapeHtml(d.teamAName) + '</button>' +
                '<button class="btn btn-ghost" data-act="walkover" data-winner="' + d.teamBId + '">Thắng trắng cho ' + Ui.escapeHtml(d.teamBName) + '</button>' +
                '<button class="btn btn-ghost" data-act="rematch">Hủy &amp; đấu lại</button>' +
                '<button class="btn btn-ghost btn-danger-text" data-act="cancel-match">Hủy trận (xử thua cả hai đội)</button>' +
                '<button class="btn btn-ghost" data-act="disqualify" data-team="' + d.teamAId + '">Tước quyền ' + Ui.escapeHtml(d.teamAName) + '</button>' +
                '<button class="btn btn-ghost" data-act="disqualify" data-team="' + d.teamBId + '">Tước quyền ' + Ui.escapeHtml(d.teamBName) + '</button>') +
                // UC-34: trọng tài / BTC cấp thẻ phạt cho đội của trận ở trang Kỷ luật của giải.
                '<a class="btn btn-ghost" href="/tournament/discipline.html?id=' + d.tournamentId + '">Cấp thẻ phạt</a>' +
                '</div></div></div>';
        }

        if (d.myCaptainTeamId && playable) {
            html += '<div class="card-etoms" style="margin-top:18px"><div class="body"><h3>Đội tôi bỏ cuộc</h3>' +
                '<button class="btn btn-ghost" data-act="forfeit" data-team="' + d.myCaptainTeamId + '">Xin bỏ cuộc trận này</button></div></div>';
        }

        root.innerHTML = html;
        // Tiêu đề trận đặt trên khối Trực tiếp (UC-45/46, match-live.js); phần còn lại ở dưới.
        var headSlot = document.getElementById('match-head');
        var head = root.querySelector('.page-head');
        // Khối kết quả ngay dưới bảng tỉ số, trên khối tỉ số trực tiếp.
        var result = root.querySelector('.match-result');
        if (headSlot && head) headSlot.replaceChildren.apply(headSlot, result ? [head, result] : [head]);
        wireActions(d);
        if (d.canManage) loadSubRequests();
        var lobbyBox = document.getElementById('lobby-box');
        if (lobbyBox && window.MatchLobby) window.MatchLobby.mount(matchId, lobbyBox);
    }

    function utc(iso) { return iso ? new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z') : null; }
    function hm(d) { return d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' }); }

    // MAT-02 / UC-21: cửa sổ điểm danh mở 30 phút trước giờ thi đấu, đóng đúng giờ thi đấu (BR-MATCH-01).
    function checkInWindowHtml(d) {
        var open = utc(d.checkInOpenAt), kick = utc(d.scheduledAt), now = new Date();
        if (!d.teamAId || !d.teamBId || (d.status !== 'PENDING' && d.status !== 'READY')) return '';
        if (!open || !kick) {
            // Trận chưa xếp giờ (giải tự hẹn giờ): đội trưởng điểm danh khi đội đã sẵn sàng.
            var free = d.myCaptainTeamId && !d.myTeamCheckedIn ? '<button class="btn btn-primary" data-act="self-checkin">Điểm danh đội tôi</button>' : '';
            return '<p class="hint mb-2">Trận chưa có giờ thi đấu. Khi đội đã sẵn sàng, đội trưởng bấm điểm danh; hai đội cùng điểm danh thì phòng chờ trận mở.</p>' +
                (free ? '<div class="btn-row mb-2">' + free + '</div>' : '');
        }
        var state = now < open ? 'Cửa sổ điểm danh mở lúc ' + hm(open) + ' (trước giờ thi đấu 30 phút).'
            : now < kick ? 'Cửa sổ điểm danh đang mở đến ' + hm(kick) + '.'
            : 'Cửa sổ điểm danh đã đóng lúc ' + hm(kick) + '.';
        var btn = '';
        if (d.myCaptainTeamId && !d.myTeamCheckedIn) {
            btn = now >= open && now < kick
                ? '<button class="btn btn-primary" data-act="self-checkin">Điểm danh đội tôi</button>'
                : '<button class="btn btn-ghost" disabled>Điểm danh đội tôi</button>';
        }
        return '<p class="hint mb-2">' + Ui.escapeHtml(state) + '</p>' + (btn ? '<div class="btn-row mb-2">' + btn + '</div>' : '');
    }

    async function withReason(label, cb) {
        var reason = await window.etomsPrompt(label, {
            title: 'Nhập lý do', placeholder: 'Lý do sẽ được ghi vào nhật ký trận đấu',
            required: true, minLength: 5, maxLength: 500
        });
        if (reason === null) return;
        cb(reason);
    }

    function wireActions(d) {
        var selfBtn = root.querySelector('[data-act="self-checkin"]');
        if (selfBtn) selfBtn.addEventListener('click', async function () {
            selfBtn.disabled = true;
            await call(Api.post('/api/matches/' + matchId + '/ops/self-checkin', {}));
        });

        root.querySelectorAll('[data-act="checkin"]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                withReason('Lý do điểm danh thủ công:', async function (reason) {
                    await call(Api.post('/api/matches/' + matchId + '/ops/checkin', { teamSide: btn.dataset.side, reason: reason }));
                });
            });
        });

        root.querySelectorAll('[data-act="walkover"]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                var ok = await etomsConfirm('Xác nhận xử thắng trắng cho đội này?');
                if (!ok) return;
                withReason('Lý do xử thắng trắng:', async function (reason) {
                    await call(Api.post('/api/matches/' + matchId + '/ops/walkover', { winnerTeamId: parseInt(btn.dataset.winner, 10), reason: reason }));
                });
            });
        });

        var rematchBtn = root.querySelector('[data-act="rematch"]');
        if (rematchBtn) rematchBtn.addEventListener('click', async function () {
            // UC-26 bước 3 / A2: cho Ban tổ chức thấy trước các trận đã đá phía sau cũng sẽ bị hủy kết quả.
            var impact = [];
            try { impact = (await Api.get('/api/matches/' + matchId + '/ops/rematch-preview')).data || []; } catch (e) { impact = []; }
            var msg = 'Hủy kết quả hiện tại và đấu lại từ đầu?';
            if (impact.length) msg += ' Các trận đã đá phía sau cũng sẽ bị hủy kết quả và đấu lại: ' +
                impact.map(function (x) { return '#' + x.matchId + ' ' + x.label + ' (' + x.status + ')'; }).join('; ') + '.';
            var ok = await etomsConfirm(msg);
            if (!ok) return;
            withReason('Lý do đấu lại:', async function (reason) {
                await call(Api.post('/api/matches/' + matchId + '/ops/rematch', { reason: reason }));
            });
        });

        var cancelBtn = root.querySelector('[data-act="cancel-match"]');
        if (cancelBtn) cancelBtn.addEventListener('click', async function () {
            var ok = await etomsConfirm('Hủy trận và xử thua cả hai đội? Không đội nào đi tiếp từ trận này: ở vòng loại trực tiếp, đối thủ ở trận sau được xử thắng trắng; ở vòng bảng, mỗi đội tính một trận thua. Thao tác này không hoàn tác được (muốn đá lại thì dùng "Hủy & đấu lại").');
            if (!ok) return;
            withReason('Lý do hủy trận:', async function (reason) {
                await call(Api.post('/api/matches/' + matchId + '/ops/cancel', { reason: reason }));
            });
        });

        root.querySelectorAll('[data-copy]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                var text = btn.getAttribute('data-copy');
                try { await navigator.clipboard.writeText(text); }
                catch (e) { var ta = document.createElement('textarea'); ta.value = text; document.body.appendChild(ta); ta.select(); try { document.execCommand('copy'); } catch (x) { } ta.remove(); }
                var old = btn.textContent; btn.textContent = 'Đã sao chép'; setTimeout(function () { btn.textContent = old; }, 1500);
            });
        });

        root.querySelectorAll('[data-act="disqualify"]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                var ok = await etomsConfirm('Xác nhận tước quyền thi đấu của đội này? Đội rời giải và mọi trận còn lại của đội được xử thắng trắng cho đối thủ.');
                if (!ok) return;
                withReason('Lý do tước quyền thi đấu:', async function (reason) {
                    await call(Api.post('/api/matches/' + matchId + '/ops/disqualify', { teamId: parseInt(btn.dataset.team, 10), reason: reason }));
                });
            });
        });

        var forfeitBtn = root.querySelector('[data-act="forfeit"]');
        if (forfeitBtn) forfeitBtn.addEventListener('click', async function () {
            var ok = await etomsConfirm('Xác nhận đội bạn xin bỏ cuộc trận này?');
            if (!ok) return;
            withReason('Lý do bỏ cuộc:', async function (reason) {
                await call(Api.post('/api/matches/' + matchId + '/ops/forfeit', { teamId: parseInt(forfeitBtn.dataset.team, 10), reason: reason }));
            });
        });

        var roomForm = root.querySelector('#roomForm');
        if (roomForm) roomForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearFieldErrors(roomForm);
            var fd = new FormData(roomForm);
            var code = String(fd.get('roomCode') || '').trim(), pw = String(fd.get('roomPassword') || '').trim();
            var errs = {};
            if (!code) errs.RoomCode = 'Vui lòng nhập mã phòng.';
            else if (code.length > 100) errs.RoomCode = 'Mã phòng tối đa 100 ký tự.';
            if (pw.length > 100) errs.RoomPassword = 'Mật khẩu phòng tối đa 100 ký tự.';
            if (Object.keys(errs).length) { Ui.applyFieldErrors(roomForm, errs); return; }
            try {
                var res = await Api.put('/api/matches/' + matchId + '/ops/room', { roomCode: code, roomPassword: pw || null });
                Ui.showAlert(flash, 'success', res.message);
                load();
            } catch (err) {
                Ui.applyFieldErrors(roomForm, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.message);
            }
        });

        var subForm = root.querySelector('#subForm');
        if (subForm) subForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            var fd = new FormData(subForm);
            if (!fd.get('outAccountId') || !fd.get('inAccountId') || !String(fd.get('reason') || '').trim()) {
                Ui.showAlert(flash, 'danger', 'Chọn người ra, người vào và ghi lý do thay người.');
                return;
            }
            await call(Api.post('/api/matches/' + matchId + '/ops/substitute-requests', {
                teamId: d.myCaptainTeamId,
                outAccountId: parseInt(fd.get('outAccountId'), 10),
                inAccountId: parseInt(fd.get('inAccountId'), 10),
                reason: fd.get('reason')
            }));
        });

    }

    async function call(promise) {
        try {
            var res = await promise;
            Ui.showAlert(flash, 'success', res.message);
            load();
        } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
    }

    async function loadSubRequests() {
        var box = document.getElementById('subList');
        if (!box) return;
        try {
            var list = await Api.get('/api/matches/' + matchId + '/ops/substitute-requests');
            if (!list.length) { box.innerHTML = '<h3>Yêu cầu thay người</h3><p class="muted">Chưa có yêu cầu nào.</p>'; return; }
            var rows = list.map(function (r) {
                var actions = r.status === 'PENDING'
                    ? '<button class="btn btn-ghost" data-approve="' + r.matchRequestId + '">Duyệt</button>' +
                      '<button class="btn btn-ghost" data-reject="' + r.matchRequestId + '">Từ chối</button>'
                    : '<span class="badge badge-soft">' + r.status + '</span>';
                return '<p>' + Ui.escapeHtml(r.outName) + ' → ' + Ui.escapeHtml(r.inName) +
                    ' (' + Ui.escapeHtml(r.reason) + ') ' + actions + '</p>';
            }).join('');
            box.innerHTML = '<h3>Yêu cầu thay người</h3>' + rows;
            box.querySelectorAll('[data-approve]').forEach(function (b) {
                b.addEventListener('click', function () { decideSub(b.dataset.approve, true); });
            });
            box.querySelectorAll('[data-reject]').forEach(function (b) {
                b.addEventListener('click', function () { decideSub(b.dataset.reject, false); });
            });
        } catch (e) { box.innerHTML = '<h3>Yêu cầu thay người</h3><p class="muted">Không tải được.</p>'; }
    }

    async function decideSub(requestId, approve) {
        await call(Api.post('/api/matches/' + matchId + '/ops/substitute-requests/' + requestId + '/decide', { approve: approve }));
        loadSubRequests();
    }

    document.addEventListener('etoms:layout-ready', async function () {
        matchId = Ui.qsInt('id');
        root = document.getElementById('match-root');
        flash = document.getElementById('flash');
        if (!matchId) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã trận đấu</h3></div></div>'; return; }
        // Khách cũng xem được trang trận (LIVE-02). API chỉ trả mã phòng và nút thao tác cho
        // trọng tài/BTC/đội trưởng, nên không cần bắt đăng nhập để xem.
        me = await Ui.currentUser();
        load();
    });
})();
