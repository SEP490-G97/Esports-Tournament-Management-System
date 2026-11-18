// MAT-07 / UC-25 — Phòng chờ trận: 2 đội trưởng + trọng tài + Ban tổ chức trao đổi mã phòng, báo trễ giờ.
// Tự làm mới 4 giây/lần khi trang đang mở (không cần tải lại trang). Tin nhắn không sửa/xóa được vì là bằng chứng
// cho người phân xử tranh chấp. Báo cáo tin nhắn xúc phạm gửi tới Ban tổ chức (A2).
window.MatchLobby = (function () {
    var REFRESH_MS = 4000;
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var matchId = null, box = null, timer = null, lastKey = null, data = null, sending = false;

    function fmt(iso) {
        var d = new Date(/Z$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z');
        var today = new Date();
        var sameDay = d.toDateString() === today.toDateString();
        return d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) +
            (sameDay ? '' : ' ' + d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' }));
    }

    function messageHtml(m) {
        var reported = m.reportStatus === 'REPORTED' ? '<span class="badge badge-danger">Đã bị báo cáo</span>'
            : m.reportStatus === 'ACTIONED' ? '<span class="badge badge-soft">Đã xử lý</span>' : '';
        return '<div class="lobby-msg ' + (m.isMine ? 'mine' : '') + ' role-' + esc(m.senderRole.toLowerCase()) + '">' +
            '<div class="lobby-meta"><span class="lobby-role">' + esc(m.senderRoleLabel) + '</span>' +
            '<span class="lobby-name">' + esc(m.isMine ? 'Bạn' : m.senderName) + '</span>' +
            '<span class="lobby-time">' + esc(fmt(m.sentAt)) + '</span>' + reported +
            (m.canReport ? '<button type="button" class="lobby-report" data-report="' + m.matchChatMessageId + '" title="Báo cáo tin nhắn xúc phạm">Báo cáo</button>' : '') +
            '</div><div class="lobby-text">' + esc(m.content) + '</div></div>';
    }

    function renderShell() {
        box.innerHTML = '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2">' +
            '<h3 class="m-0">Phòng chờ trận</h3><span class="hint" id="lobby-me"></span></div>' +
            '<div class="hint mt-1" id="lobby-state"></div>' +
            '<div class="lobby-list mt-2" id="lobby-list" aria-live="polite"><p class="muted">Đang tải...</p></div>' +
            '<form id="lobby-form" class="lobby-form" novalidate>' +
            '<textarea class="input" name="content" id="lobby-input" rows="2" placeholder="Nhập tin nhắn (Enter để gửi, Shift+Enter xuống dòng)"></textarea>' +
            '<div class="lobby-form-foot"><span class="field-error" data-field-error="Content"></span><span class="hint" id="lobby-count"></span>' +
            '<button class="btn btn-primary" type="submit">Gửi</button></div></form>';
        var form = document.getElementById('lobby-form');
        var input = document.getElementById('lobby-input');
        input.addEventListener('input', updateCount);
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); form.requestSubmit(); }
        });
        form.addEventListener('submit', send);
        document.getElementById('lobby-list').addEventListener('click', report);
    }

    function updateCount() {
        var input = document.getElementById('lobby-input');
        var max = data ? data.maxLength : 1000;
        var n = input.value.trim().length;
        var c = document.getElementById('lobby-count');
        c.textContent = n + '/' + max;
        c.classList.toggle('text-danger-etoms', n > max);
    }

    function render(d) {
        data = d;
        document.getElementById('lobby-me').textContent = 'Bạn vào với vai trò: ' + d.myRoleLabel;
        document.getElementById('lobby-state').textContent = d.stateMessage;
        var form = document.getElementById('lobby-form');
        form.hidden = !d.isOpen;
        var key = d.isOpen + '|' + d.messages.map(function (m) { return m.matchChatMessageId + ':' + m.reportStatus; }).join(',');
        if (key === lastKey) return;
        var list = document.getElementById('lobby-list');
        var atBottom = list.scrollHeight - list.scrollTop - list.clientHeight < 40;
        list.innerHTML = d.messages.length ? d.messages.map(messageHtml).join('')
            : '<p class="muted">Chưa có tin nhắn. ' + (d.isOpen ? 'Đội trưởng đội nhà hãy gửi mã phòng và mật khẩu cho đội bạn.' : '') + '</p>';
        if (atBottom || lastKey === null) list.scrollTop = list.scrollHeight;
        lastKey = key;
        updateCount();
    }

    async function refresh() {
        try { render((await Api.get('/api/matches/' + matchId + '/ops/chat')).data); }
        catch (err) {
            stop();
            box.innerHTML = '<h3>Phòng chờ trận</h3><p class="muted">' + esc(err.message || 'Không xem được phòng chờ.') + '</p>';
        }
    }

    async function send(e) {
        e.preventDefault();
        if (sending) return;
        var form = e.target, input = document.getElementById('lobby-input');
        Ui.clearFieldErrors(form);
        var text = input.value.trim(), max = data ? data.maxLength : 1000;
        if (!text) { Ui.applyFieldErrors(form, { Content: 'Vui lòng nhập nội dung tin nhắn.' }); return; }
        if (text.length > max) { Ui.applyFieldErrors(form, { Content: 'Tin nhắn tối đa ' + max + ' ký tự.' }); return; }
        sending = true;
        form.querySelector('[type=submit]').disabled = true;
        try {
            await Api.post('/api/matches/' + matchId + '/ops/chat', { content: text });
            input.value = '';
            lastKey = null;
            await refresh();
            var list = document.getElementById('lobby-list'); list.scrollTop = list.scrollHeight;
        } catch (err) {
            Ui.applyFieldErrors(form, err.fieldErrors && Object.keys(err.fieldErrors).length ? err.fieldErrors : { Content: err.message || 'Không gửi được tin nhắn.' });
        } finally {
            sending = false;
            form.querySelector('[type=submit]').disabled = false;
            updateCount();
            input.focus();
        }
    }

    async function report(e) {
        var b = e.target.closest('[data-report]');
        if (!b) return;
        var note = await window.etomsPrompt('Lý do báo cáo tin nhắn này (sẽ gửi tới Ban tổ chức):', {
            title: 'Báo cáo tin nhắn', placeholder: 'Ví dụ: xúc phạm đội bạn, lời lẽ khiêu khích', required: true, minLength: 5, maxLength: 500
        });
        if (note === null) return;
        try {
            var res = await Api.post('/api/matches/' + matchId + '/ops/chat/' + parseInt(b.dataset.report, 10) + '/report', { note: note });
            lastKey = null;
            await refresh();
            Ui.showAlert(document.getElementById('flash'), 'success', res.message);
        } catch (err) { Ui.showAlert(document.getElementById('flash'), 'danger', err.message || 'Không gửi được báo cáo.'); }
    }

    function stop() { clearTimeout(timer); timer = null; }

    function loop() {
        stop();
        timer = setTimeout(async function () {
            if (!box || !document.body.contains(box)) { stop(); return; } // trang trận đã vẽ lại, mount mới sẽ chạy tiếp
            if (document.visibilityState === 'visible') await refresh();
            if (timer !== null) loop();
        }, REFRESH_MS);
    }

    // Trang trận vẽ lại toàn bộ sau mỗi thao tác → gắn lại vào khung mới, giữ nội dung đang gõ dở.
    function mount(id, container) {
        var draft = document.getElementById('lobby-input') ? document.getElementById('lobby-input').value : '';
        matchId = id; box = container; lastKey = null;
        renderShell();
        if (draft) document.getElementById('lobby-input').value = draft;
        refresh().then(function () {
            // Đến từ nút "Vào phòng chờ" ở trang đội (#lobby-box): phòng chờ vẽ sau khi tải nên phải tự cuộn tới.
            if (location.hash === '#lobby-box' && !mount.scrolled) { mount.scrolled = true; box.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
        });
        loop();
    }

    return { mount: mount };
})();
