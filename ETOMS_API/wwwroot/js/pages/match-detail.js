// STUB TẠM thay module MAT (Vũ Đức Hải: MAT-01/04/05/06/07, Nguyễn Quang Anh: MAT-08..12) —
// chỉ đủ để demo luồng vào trận, không phải bài làm cá nhân của hai bạn.
(function () {
    var matchId = null;
    var me = null;
    var root, flash;

    var STATUS_LABEL = {
        PENDING: 'Chờ điểm danh', READY: 'Sẵn sàng', LIVE: 'Đang diễn ra',
        WAITING_REPORT: 'Chờ báo cáo tỉ số', DISPUTED: 'Đang tranh chấp',
        VERIFIED: 'Đã có kết quả', WALKOVER: 'Thắng trắng / Xử thua', CANCELLED: 'Đã hủy'
    };

    function statusLabel(s) { return STATUS_LABEL[s] || s; }

    async function load() {
        var detail;
        try { detail = await Api.get('/api/matches/' + matchId + '/ops'); }
        catch (e) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy trận đấu</h3></div></div>'; return; }
        render(detail);
    }

    function render(d) {
        var html = '' +
            '<div class="page-head"><h1>' + Ui.escapeHtml(d.teamAName) + ' vs ' + Ui.escapeHtml(d.teamBName) + '</h1>' +
            '<p>Bo' + d.bestOf + ' · ' + statusLabel(d.status) +
            (d.scheduledAt ? ' · Lịch: ' + new Date(/Z$|[+-]\d\d:\d\d$/.test(d.scheduledAt) ? d.scheduledAt : d.scheduledAt + 'Z').toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : '') + '</p></div>';

        if (d.winnerTeamId) {
            html += '<div class="alert alert-success">Đội thắng: ' +
                Ui.escapeHtml(d.winnerTeamId === d.teamAId ? d.teamAName : d.teamBName) + '</div>';
        }

        html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Điểm danh</h3>' +
            '<p>' + Ui.escapeHtml(d.teamAName) + ': ' + (d.teamACheckedIn ? '<span class="badge badge-active">Đã điểm danh</span>' : '<span class="badge badge-soft">Chưa</span>') + '</p>' +
            '<p>' + Ui.escapeHtml(d.teamBName) + ': ' + (d.teamBCheckedIn ? '<span class="badge badge-active">Đã điểm danh</span>' : '<span class="badge badge-soft">Chưa</span>') + '</p>' +
            checkInWindowHtml(d);
        if (d.canManage) {
            html += '<div class="btn-row">' +
                (d.teamACheckedIn ? '' : '<button class="btn btn-ghost" data-act="checkin" data-side="A">Điểm danh thủ công Đội A</button>') +
                (d.teamBCheckedIn ? '' : '<button class="btn btn-ghost" data-act="checkin" data-side="B">Điểm danh thủ công Đội B</button>') +
                '</div>';
        }
        html += '</div></div>';

        if (d.canManage || d.myCaptainTeamId) {
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

        if (d.myCaptainTeamId) {
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Xin thay người dự bị</h3>' +
                '<form id="subForm" class="btn-row" style="align-items:flex-end">' +
                '<div><label>Mã tài khoản ra</label><input class="input" name="outAccountId" type="number" required></div>' +
                '<div><label>Mã tài khoản vào</label><input class="input" name="inAccountId" type="number" required></div>' +
                '<div style="flex:1"><label>Lý do</label><input class="input" name="reason" required></div>' +
                '<button class="btn btn-primary" type="submit">Gửi yêu cầu</button></form></div></div>';
        }

        if (d.canManage) {
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body" id="subList"><h3>Yêu cầu thay người</h3><p class="muted">Đang tải...</p></div></div>';
        }

        if (d.canManage || d.myCaptainTeamId) {
            // Phòng chờ (MAT-07 / UC-25) do match-lobby.js vẽ và tự làm mới.
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body" id="lobby-box"></div></div>';
        }

        if (d.canManage) {
            html += '<div class="card-etoms"><div class="body"><h3>Xử lý trận (BTC/trọng tài)</h3><div class="btn-row">' +
                '<button class="btn btn-ghost" data-act="walkover" data-winner="' + d.teamAId + '">Thắng trắng cho ' + Ui.escapeHtml(d.teamAName) + '</button>' +
                '<button class="btn btn-ghost" data-act="walkover" data-winner="' + d.teamBId + '">Thắng trắng cho ' + Ui.escapeHtml(d.teamBName) + '</button>' +
                '<button class="btn btn-ghost" data-act="rematch">Hủy &amp; đấu lại</button>' +
                '<button class="btn btn-ghost" data-act="disqualify" data-team="' + d.teamAId + '">Tước quyền ' + Ui.escapeHtml(d.teamAName) + '</button>' +
                '<button class="btn btn-ghost" data-act="disqualify" data-team="' + d.teamBId + '">Tước quyền ' + Ui.escapeHtml(d.teamBName) + '</button>' +
                '</div></div></div>';
        }

        if (d.myCaptainTeamId) {
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
                withReason('Lý do check-in thủ công:', async function (reason) {
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
            var ok = await etomsConfirm('Hủy kết quả hiện tại và đấu lại từ đầu?');
            if (!ok) return;
            withReason('Lý do đấu lại:', async function (reason) {
                await call(Api.post('/api/matches/' + matchId + '/ops/rematch', { reason: reason }));
            });
        });

        root.querySelectorAll('[data-act="disqualify"]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                var ok = await etomsConfirm('Xác nhận tước quyền thi đấu của đội này?');
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
