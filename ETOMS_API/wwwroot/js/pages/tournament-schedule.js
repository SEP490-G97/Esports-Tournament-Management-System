(function () {
    var tournamentId = null;

    function flash() { return document.getElementById('flash'); }

    function showEmpty(title, message) {
        document.getElementById('schedule-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    // Server trả giờ UTC không kèm Z.
    function fromServer(value) {
        return value ? new Date(/Z$/.test(value) ? value : value + 'Z') : null;
    }

    function formatTime(value) {
        var date = fromServer(value);
        return date ? date.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }) : '';
    }

    function toLocalInput(date) {
        var pad = function (n) { return String(n).padStart(2, '0'); };
        return date.getFullYear() + '-' + pad(date.getMonth() + 1) + '-' + pad(date.getDate()) +
            'T' + pad(date.getHours()) + ':' + pad(date.getMinutes());
    }

    // Ô datetime-local trả giờ máy người dùng; gửi lên server dạng UTC.
    function toUtc(value) {
        return value ? new Date(value).toISOString() : null;
    }

    function openPicker(input) {
        try { input.showPicker(); } catch (e) { /* trình duyệt cũ vẫn dùng được biểu tượng lịch */ }
    }

    function timeInput(attrs, value, min) {
        return '<input type="datetime-local" class="input" style="max-width:230px" ' + attrs +
            ' value="' + Ui.escapeHtml(value || '') + '" min="' + Ui.escapeHtml(min) + '" />';
    }

    function matchRow(m, min) {
        var when = m.scheduledAt
            ? '<div class="rl"><strong>' + Ui.escapeHtml(formatTime(m.scheduledAt)) + '</strong> · Check-in mở lúc ' + Ui.escapeHtml(formatTime(m.checkInOpenAt)) + '</div>'
            : '<div class="rl">Chưa xếp giờ</div>';
        var referee = '<div class="rl">' + Ui.escapeHtml(m.refereeName ? 'Trọng tài: ' + m.refereeName : 'Chưa có trọng tài, Ban tổ chức phân xử') + '</div>';
        var editor = m.canEdit
            ? '<div class="d-flex flex-wrap align-items-center gap-2">' +
                timeInput('data-time="' + m.matchId + '" aria-label="Giờ thi đấu ' + Ui.escapeHtml(m.label) + '"', m.scheduledAt ? toLocalInput(fromServer(m.scheduledAt)) : '', min) +
                '<button type="button" class="btn btn-primary" data-save="' + m.matchId + '">Lưu giờ</button>' +
                (m.scheduledAt ? '<button type="button" class="btn btn-ghost" data-clear="' + m.matchId + '">Bỏ giờ</button>' : '') +
              '</div>'
            : '<div class="hint">' + Ui.escapeHtml(m.lockedReason || '') + '</div>';

        return '<div class="member-card" style="flex-direction:column;align-items:stretch;gap:8px;margin-bottom:10px" data-match="' + m.matchId + '">' +
            '<div class="d-flex flex-wrap justify-content-between align-items-start gap-2">' +
            '<div class="grow"><div class="nm">' + Ui.escapeHtml(m.label) + ': ' + Ui.escapeHtml(m.teamAName) + ' vs ' + Ui.escapeHtml(m.teamBName) + '</div>' +
            when + referee + '</div>' +
            '<span class="badge ' + Ui.escapeHtml(m.statusClass) + '">' + Ui.escapeHtml(m.statusLabel) + '</span></div>' +
            editor +
            '<span class="field-error" data-row-error="' + m.matchId + '"></span>' +
            '</div>';
    }

    function roundCard(r, board, min) {
        var editable = board.canEdit && r.matches.some(function (m) { return m.canEdit; });
        var key = r.stageNo + '-' + r.roundNo;
        var bulk = editable && r.matches.length > 1
            ? '<div class="d-flex flex-wrap align-items-end gap-2" style="margin-bottom:14px">' +
                '<div class="field" style="margin:0"><label for="first-' + key + '">Giờ trận đầu</label>' +
                timeInput('id="first-' + key + '" data-first="' + key + '"', '', min) + '</div>' +
                '<div class="field" style="margin:0"><label for="gap-' + key + '">Cách nhau (phút)</label>' +
                '<input id="gap-' + key + '" type="number" class="input" style="max-width:120px" min="0" max="1440" value="60" /></div>' +
                '<button type="button" class="btn btn-ghost" data-round="' + key + '" data-stage="' + r.stageNo + '" data-round-no="' + r.roundNo + '">Xếp cả vòng</button>' +
              '</div>'
            : '';
        var scheduled = r.matches.filter(function (m) { return m.scheduledAt; }).length;
        return '<div class="card-etoms" style="margin-top:16px"><div class="body">' +
            '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2" style="margin-bottom:12px">' +
            '<div class="section-title" style="margin:0">' + Ui.escapeHtml(r.label) + '</div>' +
            '<span class="rl">' + scheduled + '/' + r.matches.length + ' trận đã có giờ</span></div>' +
            bulk + r.matches.map(function (m) { return matchRow(m, min); }).join('') +
            '</div></div>';
    }

    function render(board) {
        document.getElementById('page-title').textContent = 'Xếp lịch · ' + board.tournamentName;
        document.getElementById('page-sub').textContent = 'Thời gian thi đấu: từ ' + formatTime(board.startAt) +
            (board.endAt ? ' đến ' + formatTime(board.endAt) : '') + '. Check-in mở trước giờ thi đấu 30 phút.';

        // Giờ chọn sớm nhất: sau hiện tại 30 phút và không trước ngày khởi tranh.
        var earliest = new Date(Date.now() + 30 * 60000);
        var start = fromServer(board.startAt);
        if (start > earliest) earliest = start;
        var min = toLocalInput(earliest);

        var notice = board.blockedReason ? '<div class="pending-banner">' + Ui.escapeHtml(board.blockedReason) + '</div>' : '';
        var summary = '<div class="card-etoms"><div class="body">' + notice +
            '<div id="schedule-summary">' + (board.matchCount - board.unscheduledCount) + '/' + board.matchCount + ' trận đã có giờ thi đấu.</div>' +
            '<div class="btn-row"><a class="btn btn-ghost" href="/tournament/manage.html?id=' + tournamentId + '">Quản lý giải</a>' +
            '<a class="btn btn-ghost" href="/tournament/referees.html?id=' + tournamentId + '">Gán trọng tài</a></div></div></div>';

        document.getElementById('schedule-root').innerHTML = board.matchCount
            ? summary + board.rounds.map(function (r) { return roundCard(r, board, min); }).join('')
            : summary.replace('trận đã có giờ thi đấu.', 'trận. Giải chưa có nhánh đấu.');

        wire();
    }

    function wire() {
        document.querySelectorAll('input[type=datetime-local]').forEach(function (input) {
            input.addEventListener('click', function () { openPicker(input); });
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Tab') return;
                e.preventDefault();
                if (e.key === 'Enter' || e.key === ' ') openPicker(input);
            });
        });
        document.querySelectorAll('[data-save]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var id = btn.dataset.save;
                var value = document.querySelector('[data-time="' + id + '"]').value;
                if (!value) { showRowError(id, 'Vui lòng chọn giờ thi đấu.'); return; }
                save(id, toUtc(value));
            });
        });
        document.querySelectorAll('[data-clear]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                var ok = window.etomsConfirm ? await window.etomsConfirm('Bỏ giờ thi đấu của trận này? Hai đội sẽ được báo.') : true;
                if (ok) save(btn.dataset.clear, null);
            });
        });
        document.querySelectorAll('[data-round]').forEach(function (btn) {
            btn.addEventListener('click', function () { scheduleRound(btn); });
        });
    }

    function showRowError(matchId, message) {
        var el = document.querySelector('[data-row-error="' + matchId + '"]');
        if (el) el.textContent = message;
    }

    async function save(matchId, scheduledAt) {
        Ui.clearAlert(flash());
        try {
            var res = await Api.put('/api/tournaments/' + tournamentId + '/schedule/matches/' + matchId, { scheduledAt: scheduledAt });
            render(res.data);
            Ui.showAlert(flash(), 'success', res.message);
        } catch (err) {
            var msg = (err.fieldErrors && err.fieldErrors.ScheduledAt) || err.message;
            showRowError(matchId, msg);
            Ui.showAlert(flash(), 'danger', msg);
        }
    }

    async function scheduleRound(btn) {
        var key = btn.dataset.round;
        var first = document.getElementById('first-' + key).value;
        if (!first) { Ui.showAlert(flash(), 'danger', 'Vui lòng chọn giờ trận đầu của vòng.'); return; }
        Ui.clearAlert(flash());
        try {
            var res = await Api.post('/api/tournaments/' + tournamentId + '/schedule/round', {
                stageNo: parseInt(btn.dataset.stage, 10),
                roundNo: parseInt(btn.dataset.roundNo, 10),
                firstAt: toUtc(first),
                gapMinutes: parseInt(document.getElementById('gap-' + key).value || '0', 10)
            });
            render(res.data.board);
            var skipped = res.data.skipped.length
                ? ' ' + res.data.skipped.join(' ')
                : '';
            Ui.showAlert(flash(), res.data.skipped.length ? 'danger' : 'success', res.message + skipped);
        } catch (err) {
            Ui.showAlert(flash(), 'danger', err.message);
        }
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
            var res = await Api.get('/api/tournaments/' + tournamentId + '/schedule');
            render(res.data);
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể xếp lịch cho giải này', 'Chỉ Ban tổ chức của giải được xếp lịch thi đấu.');
            else showEmpty('Không tải được lịch thi đấu', err.message);
        }
    });
})();
