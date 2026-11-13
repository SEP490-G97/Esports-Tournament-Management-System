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
            (d.scheduledAt ? ' · Lịch: ' + new Date(d.scheduledAt).toLocaleString('vi-VN') : '') + '</p></div>';

        if (d.winnerTeamId) {
            html += '<div class="alert alert-success">Đội thắng: ' +
                Ui.escapeHtml(d.winnerTeamId === d.teamAId ? d.teamAName : d.teamBName) + '</div>';
        }

        html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Điểm danh</h3>' +
            '<p>Đội A: ' + (d.teamACheckedIn ? '<span class="badge badge-active">Đã điểm danh</span>' : '<span class="badge badge-soft">Chưa</span>') + '</p>' +
            '<p>Đội B: ' + (d.teamBCheckedIn ? '<span class="badge badge-active">Đã điểm danh</span>' : '<span class="badge badge-soft">Chưa</span>') + '</p>';
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
            if (d.canManage) {
                html += '<form id="roomForm" class="btn-row" style="align-items:flex-end">' +
                    '<div><label>Mã phòng</label><input class="input" name="roomCode" required></div>' +
                    '<div><label>Mật khẩu (tuỳ chọn)</label><input class="input" name="roomPassword"></div>' +
                    '<button class="btn btn-primary" type="submit">Lưu</button></form>';
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
            html += '<div class="card-etoms" style="margin-bottom:18px"><div class="body"><h3>Phòng chờ trận</h3>' +
                '<div id="chatBox" style="max-height:260px;overflow:auto;margin-bottom:12px"></div>' +
                '<form id="chatForm" class="btn-row">' +
                '<input class="input" style="flex:1" name="content" placeholder="Nhập tin nhắn..." required>' +
                '<button class="btn btn-ghost" type="submit">Gửi</button></form></div></div>';
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
        wireActions(d);
        if (d.canManage) loadSubRequests();
        if (d.canManage || d.myCaptainTeamId) loadChat();
    }

    async function withReason(label, cb) {
        var reason = prompt(label);
        if (reason === null) return;
        if (!reason.trim()) { Ui.showAlert(flash, 'danger', 'Vui lòng nhập lý do.'); return; }
        cb(reason.trim());
    }

    function wireActions(d) {
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
            var fd = new FormData(roomForm);
            await call(Api.put('/api/matches/' + matchId + '/ops/room', { roomCode: fd.get('roomCode'), roomPassword: fd.get('roomPassword') || null }));
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

        var chatForm = root.querySelector('#chatForm');
        if (chatForm) chatForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            var fd = new FormData(chatForm);
            try {
                await Api.post('/api/matches/' + matchId + '/ops/chat', { content: fd.get('content') });
                chatForm.reset();
                loadChat();
            } catch (err) { Ui.showAlert(flash, 'danger', err.message); }
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

    async function loadChat() {
        var box = document.getElementById('chatBox');
        if (!box) return;
        try {
            var res = await Api.get('/api/matches/' + matchId + '/ops/chat');
            var list = res.data || [];
            box.innerHTML = list.map(function (m) {
                return '<p><b>' + Ui.escapeHtml(m.senderName) + ':</b> ' + Ui.escapeHtml(m.content) + '</p>';
            }).join('') || '<p class="muted">Chưa có tin nhắn.</p>';
            box.scrollTop = box.scrollHeight;
        } catch (e) { box.innerHTML = '<p class="muted">Không xem được phòng chờ.</p>'; }
    }

    document.addEventListener('etoms:layout-ready', async function () {
        matchId = Ui.qsInt('id');
        root = document.getElementById('match-root');
        flash = document.getElementById('flash');
        if (!matchId) { root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã trận đấu</h3></div></div>'; return; }
        me = await Ui.requireAuth();
        if (!me) return;
        load();
    });
})();
