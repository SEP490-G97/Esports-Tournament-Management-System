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

    // Góp ý 22/11: "trọng tài chưa thấy nhập tỷ số, chưa thấy đẩy nhánh đi tiếp". Theo LI-01 tỉ số CHỈ vào hệ thống qua
    // 2 báo cáo mù của 2 đội trưởng (khớp nhau → trận tự xác nhận, đội thắng tự vào vòng sau) hoặc phán quyết tranh chấp
    // của trọng tài / BTC. Khối này cho mỗi vai trò biết trận đang chờ gì và nút đúng việc của mình.
    function resultCardHtml(d) {
        var done = d.status === 'VERIFIED' || d.status === 'WALKOVER';
        var open = ['PENDING', 'READY', 'LIVE', 'WAITING_REPORT'].indexOf(d.status) >= 0;
        var bothTeams = d.teamAId && d.teamBId;
        var chip = function (ok, name) {
            return '<span class="badge ' + (ok ? 'badge-active' : 'badge-soft') + '">' + Ui.escapeHtml(name) + ': ' + (ok ? 'đã nộp' : 'chưa nộp') + '</span>';
        };
        var body = '';
        if (done) {
            body = '<p class="mb-1">Tỉ số chung cuộc: <b>' + Ui.escapeHtml(d.teamAName) + ' ' + (d.scoreA ?? '–') + ' – ' + (d.scoreB ?? '–') + ' ' + Ui.escapeHtml(d.teamBName) + '</b>' +
                (d.status === 'WALKOVER' ? ' (thắng trắng)' : '') + '.</p><p class="hint mb-0">Đội thắng đã được đưa vào trận tiếp theo của nhánh đấu.</p>';
        } else if (!bothTeams) {
            body = '<p class="hint mb-0">Trận chưa đủ hai đội — chờ kết quả các trận trước. Đội thắng ở trận trước sẽ tự được đưa vào đây.</p>';
        } else if (d.openDisputeId) {
            body = '<p>Hai bản báo cáo không khớp nên trận đang <b>tranh chấp</b>. ' +
                (d.canManage ? 'Trọng tài / Ban tổ chức xem bằng chứng và ra phán quyết tỉ số; đội thắng sẽ tự vào vòng sau.' : 'Trọng tài đang xem xét bằng chứng.') + '</p>' +
                '<div class="btn-row">' + (d.canManage ? '<a class="btn btn-primary" href="/result/dispute.html?id=' + d.openDisputeId + '">Xử lý tranh chấp</a>' : '') +
                (d.myCaptainTeamId ? '<a class="btn btn-ghost" href="/result/evidence.html?matchId=' + d.matchId + '">Bổ sung bằng chứng</a>' : '') + '</div>';
        } else if (open) {
            body = '<p class="mb-2">Sau trận, <b>mỗi đội trưởng tự báo cáo tỉ số</b> (không thấy báo cáo của đội kia). Hai bản khớp nhau thì trận tự xác nhận và ' +
                'đội thắng tự vào vòng sau; lệch nhau thì thành tranh chấp để trọng tài phán quyết.</p>' +
                '<div class="d-flex flex-wrap gap-2 mb-2">' + chip(d.teamAReported, d.teamAName) + chip(d.teamBReported, d.teamBName) + '</div>' +
                (d.reportDeadlineAt ? '<p class="hint">Hạn báo cáo: ' + new Date(/Z$/.test(d.reportDeadlineAt) ? d.reportDeadlineAt : d.reportDeadlineAt + 'Z').toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) + '. Quá hạn mà chưa đủ hai bản thì trận thành tranh chấp "Quá hạn báo cáo" để trọng tài phán quyết.</p>' : '') +
                '<div class="btn-row">' +
                (d.myCaptainTeamId ? '<a class="btn btn-primary" href="/result/submit.html?matchId=' + d.matchId + '">' + ((d.myCaptainTeamId === d.teamAId ? d.teamAReported : d.teamBReported) ? 'Xem báo cáo đội tôi đã nộp' : 'Báo cáo tỉ số') + '</a>' : '') +
                (d.canManage ? '<a class="btn btn-ghost" href="/result/submit.html?matchId=' + d.matchId + '">Xem tình trạng báo cáo</a>' : '') + '</div>' +
                (d.isReferee ? '<p class="hint mt-2 mb-0">Bạn là trọng tài trận này: không nhập tỉ số thay đội (LI-01). Việc của trọng tài là điểm danh, phòng đấu, xử lý vi phạm (thắng trắng, tước quyền, thẻ phạt) và phán quyết khi có tranh chấp.</p>' : '');
        } else {
            body = '<p class="hint mb-0">Trận đã hủy.</p>';
        }
        return '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Kết quả trận</h3>' +
            (d.refereeName ? '<p class="hint">Trọng tài: <b>' + Ui.escapeHtml(d.refereeName) + '</b></p>' : '<p class="hint">Trận chưa có trọng tài — Ban tổ chức xử lý.</p>') +
            body + '</div></div>';
    }

    function render(d) {
        // Trận đã xong / đã hủy thì ẩn các thao tác chỉ dùng khi trận còn diễn ra (thay người, bỏ cuộc, xử thắng trắng...).
        var bothTeams = !!(d.teamAId && d.teamBId);
        // Chưa đủ hai đội (chờ đội thắng trận trước) thì chưa có gì để thay người / bỏ cuộc / nhập phòng.
        var playable = bothTeams && ['PENDING', 'READY', 'LIVE'].indexOf(d.status) >= 0;
        var closed = ['VERIFIED', 'WALKOVER', 'CANCELLED'].indexOf(d.status) >= 0;
        var html = '' +
            '<div class="page-head"><h1>' + Ui.escapeHtml(d.teamAName) + ' vs ' + Ui.escapeHtml(d.teamBName) + '</h1>' +
            '<p>Bo' + d.bestOf + ' · ' + statusLabel(d.status) +
            (d.scheduledAt ? ' · Lịch: ' + new Date(/Z$|[+-]\d\d:\d\d$/.test(d.scheduledAt) ? d.scheduledAt : d.scheduledAt + 'Z').toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : '') + '</p></div>';

        if (d.winnerTeamId) {
            html += '<div class="alert alert-success">Đội thắng: ' +
                Ui.escapeHtml(d.winnerTeamId === d.teamAId ? d.teamAName : d.teamBName) + '</div>';
        }

        html += resultCardHtml(d);

        html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Điểm danh</h3>' +
            '<p>' + Ui.escapeHtml(d.teamAName) + ': ' + (d.teamACheckedIn ? '<span class="badge badge-active">Đã điểm danh</span>' : '<span class="badge badge-soft">Chưa</span>') + '</p>' +
            '<p>' + Ui.escapeHtml(d.teamBName) + ': ' + (d.teamBCheckedIn ? '<span class="badge badge-active">Đã điểm danh</span>' : '<span class="badge badge-soft">Chưa</span>') + '</p>' +
            checkInWindowHtml(d);
        if (d.canManage && playable) {
            html += '<div class="btn-row">' +
                (d.teamACheckedIn ? '' : '<button class="btn btn-ghost" data-act="checkin" data-side="A">Điểm danh thủ công Đội A</button>') +
                (d.teamBCheckedIn ? '' : '<button class="btn btn-ghost" data-act="checkin" data-side="B">Điểm danh thủ công Đội B</button>') +
                '</div>';
        }
        html += '</div></div>';

        if ((d.canManage || d.myCaptainTeamId) && bothTeams && !(closed && !d.roomCode)) {
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Phòng đấu</h3>';
            if (d.roomCode) {
                html += '<p>Mã phòng: <b>' + Ui.escapeHtml(d.roomCode) + '</b>' +
                    (d.roomPassword ? ' · Mật khẩu: <b>' + Ui.escapeHtml(d.roomPassword) + '</b>' : '') + '</p>';
            } else {
                html += '<p class="muted">Chưa có thông tin phòng.</p>';
            }
            if (d.canSetRoom) {
                // UC-25 bước 3: đội trưởng đội nhà (hoặc trọng tài/BTC) nhập mã phòng; hai đội trưởng và trọng tài cùng thấy.
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
                '<div style="flex:1 1 240px"><label>Lý do</label><input class="input" name="reason" maxlength="500" required placeholder="Ví dụ: tuyển thủ bị ốm">' +
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
            html += '<div class="card-etoms"><div class="body"><h3>Xử lý trận (BTC/trọng tài)</h3><div class="btn-row">' +
            (closed || !bothTeams ? '' :
                '<button class="btn btn-ghost" data-act="walkover" data-winner="' + d.teamAId + '">Thắng trắng cho ' + Ui.escapeHtml(d.teamAName) + '</button>' +
                '<button class="btn btn-ghost" data-act="walkover" data-winner="' + d.teamBId + '">Thắng trắng cho ' + Ui.escapeHtml(d.teamBName) + '</button>' +
                '<button class="btn btn-ghost" data-act="rematch">Hủy &amp; đấu lại</button>' +
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
        if (headSlot && head) headSlot.replaceChildren(head);
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
        if (!open || !kick || (d.status !== 'PENDING' && d.status !== 'READY')) return '';
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
