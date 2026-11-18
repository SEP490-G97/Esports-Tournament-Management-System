// UC-45 (LIVE-01/02/04) + UC-46 trên trang trận: livestream gán vào trận, tỉ số từng ván,
// form nhập tỉ số cho Trọng tài của trận / Ban tổ chức. Tự làm mới 10 giây/lần khi trận đang diễn ra.
(function () {
    var REFRESH_MS = 10000;
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var matchId, root, live = null, timer = null, shownStreamKey = null, chosenStreamId = null;

    function streamKey(d) { return chosenStreamId + '#' + d.streams.map(function (s) { return s.livestreamId + ':' + s.status + ':' + s.streamUrl; }).join('|'); }

    // Nhiều kênh (ví dụ một kênh đang phát + một bản đã kết thúc): nút chuyển kênh ngay trong trang,
    // máy chủ đã xếp Đang phát → Sắp phát → Đã kết thúc nên mặc định mở kênh đang phát.
    function renderStreams(d) {
        var box = document.getElementById('live-streams');
        if (!d.streams.some(function (s) { return s.livestreamId === chosenStreamId; }))
            chosenStreamId = d.streams.length ? d.streams[0].livestreamId : null;
        var key = streamKey(d);
        if (key === shownStreamKey) return; // không nạp lại iframe khi chỉ tỉ số đổi
        shownStreamKey = key;
        if (!d.streams.length) { box.innerHTML = ''; box.hidden = true; return; }
        box.hidden = false;
        var main = d.streams.find(function (s) { return s.livestreamId === chosenStreamId; });
        var tabs = d.streams.length > 1
            ? '<div class="stream-tabs" role="tablist" aria-label="Chọn kênh phát">' + d.streams.map(function (s) {
                var on = s.livestreamId === chosenStreamId;
                return '<button type="button" role="tab" class="stream-tab' + (on ? ' active' : '') + '" aria-selected="' + on + '" data-choose-stream="' + s.livestreamId + '">' +
                    '<span class="text-truncate">' + esc(s.title || s.platformLabel) + '</span>' + LivePlayer.streamBadge(s) + '</button>';
            }).join('') + '</div>'
            : '';
        box.innerHTML = '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Livestream trận đấu</div>' +
            tabs + LivePlayer.playerHtml(main) + '</div></section>';
    }

    function renderScore(d) {
        document.getElementById('live-score-body').innerHTML = LivePlayer.scoreHtml(d);
        var u = document.getElementById('live-updated');
        if (u) u.textContent = 'Cập nhật lúc ' + new Date().toLocaleTimeString('vi-VN');
    }

    function editorHtml(d) {
        if (!d.canEdit) return '';
        if (!d.isScoringOpen) return '<div class="pending-banner mt-3 mb-0">' + esc(d.scoringClosedReason) + '</div>';
        var options = d.games.map(function (g) { return g.gameNo; });
        if (d.nextGameNo) options.push(d.nextGameNo);
        if (!options.length) return '';
        var selected = d.nextGameNo || options[options.length - 1];
        return '<form id="live-form" class="live-form mt-3" novalidate>' +
            '<div class="section-title mt-0">Nhập tỉ số (Trọng tài / Ban tổ chức)</div>' +
            '<div class="live-form-grid">' +
            '<div class="field"><label for="lf-game">Ván</label><select id="lf-game" name="GameNo" class="select">' +
            options.map(function (n) { return '<option value="' + n + '"' + (n === selected ? ' selected' : '') + '>Ván ' + n + (n === d.nextGameNo ? ' (mới)' : '') + '</option>'; }).join('') +
            '</select><span class="field-error" data-field-error="GameNo"></span></div>' +
            '<div class="field"><label for="lf-a">' + esc(d.teamAName) + '</label><input id="lf-a" name="ScoreA" type="number" min="0" max="999" step="1" class="input" inputmode="numeric" />' +
            '<span class="field-error" data-field-error="ScoreA"></span></div>' +
            '<div class="field"><label for="lf-b">' + esc(d.teamBName) + '</label><input id="lf-b" name="ScoreB" type="number" min="0" max="999" step="1" class="input" inputmode="numeric" />' +
            '<span class="field-error" data-field-error="ScoreB"></span></div>' +
            '</div><div class="btn-row">' +
            '<button type="submit" class="btn btn-primary">Lưu tỉ số ván</button>' +
            (d.games.length ? '<button type="button" class="btn btn-ghost" id="lf-clear">Xóa ván cuối (ván ' + d.games[d.games.length - 1].gameNo + ')</button>' : '') +
            '</div></form>';
    }

    function fillFromGame(form) {
        var no = parseInt(form.GameNo.value, 10);
        var g = live.games.find(function (x) { return x.gameNo === no; });
        form.ScoreA.value = g ? g.scoreA : '';
        form.ScoreB.value = g ? g.scoreB : '';
    }

    // Kiểm tra phía trình duyệt — cùng thông báo với máy chủ.
    function validate(form) {
        var errors = {};
        ['ScoreA', 'ScoreB'].forEach(function (k, i) {
            var raw = form[k].value.trim();
            var name = i === 0 ? live.teamAName : live.teamBName;
            if (raw === '') errors[k] = 'Vui lòng nhập tỉ số của ' + name + '.';
            else if (!/^\d+$/.test(raw) || parseInt(raw, 10) > 999) errors[k] = 'Tỉ số mỗi ván phải từ 0 đến 999.';
        });
        if (!errors.ScoreA && !errors.ScoreB && parseInt(form.ScoreA.value, 10) === parseInt(form.ScoreB.value, 10))
            errors.ScoreB = 'Mỗi ván phải có đội thắng: hai tỉ số không được bằng nhau.';
        return errors;
    }

    function renderEditor(d) {
        var box = document.getElementById('live-editor');
        box.innerHTML = editorHtml(d);
        var form = document.getElementById('live-form');
        if (!form) return;
        fillFromGame(form);
        form.GameNo.addEventListener('change', function () { Ui.clearFieldErrors(form); fillFromGame(form); });
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearFieldErrors(form);
            var errors = validate(form);
            if (Object.keys(errors).length) { Ui.applyFieldErrors(form, errors); return; }
            var btn = form.querySelector('[type=submit]');
            btn.disabled = true;
            try {
                var res = await Api.put('/api/matches/' + matchId + '/live/games/' + parseInt(form.GameNo.value, 10), {
                    ScoreA: parseInt(form.ScoreA.value, 10), ScoreB: parseInt(form.ScoreB.value, 10)
                });
                show(res.data, true);
                Ui.showAlert(document.getElementById('live-flash'), 'success', res.message);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(document.getElementById('live-flash'), 'danger', err.message || 'Không lưu được tỉ số.');
            } finally { btn.disabled = false; }
        });
        var clear = document.getElementById('lf-clear');
        if (clear) clear.addEventListener('click', async function () {
            var last = live.games[live.games.length - 1].gameNo;
            var ok = await window.etomsConfirm('Xóa tỉ số ván ' + last + '? Dùng khi nhập nhầm; người xem sẽ thấy tỉ số cập nhật lại.');
            if (!ok) return;
            try {
                var res = await Api.del('/api/matches/' + matchId + '/live/games/' + last);
                show(res.data, true);
                Ui.showAlert(document.getElementById('live-flash'), 'success', res.message);
            } catch (err) {
                Ui.showAlert(document.getElementById('live-flash'), 'danger', err.message || 'Không xóa được.');
            }
        });
    }

    function show(d, force) {
        var editorChanged = !live || force || live.status !== d.status || live.nextGameNo !== d.nextGameNo ||
            live.games.length !== d.games.length || live.isScoringOpen !== d.isScoringOpen;
        var changed = !live || force || live.version !== d.version;
        live = d;
        if (changed) { renderStreams(d); renderScore(d); }
        else { var u = document.getElementById('live-updated'); if (u) u.textContent = 'Cập nhật lúc ' + new Date().toLocaleTimeString('vi-VN'); }
        // Không vẽ lại form khi người nhập đang gõ.
        var editor = document.getElementById('live-editor');
        if (editorChanged && !(editor && editor.contains(document.activeElement))) renderEditor(d);
        if (!timer) schedule(); // ví dụ trận vừa chuyển sang Đang đấu sau khi lưu ván đầu
    }

    function schedule() {
        clearTimeout(timer);
        timer = null;
        if (!live || ['READY', 'LIVE', 'WAITING_REPORT'].indexOf(live.status) < 0) return;
        timer = setTimeout(async function () {
            if (document.visibilityState === 'visible') {
                try { show((await Api.get('/api/matches/' + matchId + '/live')).data, false); } catch (e) { /* thử lại lượt sau */ }
            }
            schedule();
        }, REFRESH_MS);
    }

    document.addEventListener('DOMContentLoaded', async function () {
        matchId = Ui.qsInt('id');
        root = document.getElementById('live-root');
        if (!root || !matchId) return;
        root.innerHTML = '<div id="live-streams" class="mb-3" hidden></div>' +
            '<section class="card-etoms"><div class="body">' +
            '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2"><div class="section-title m-0">Tỉ số trực tiếp</div>' +
            '<span class="hint" id="live-updated"></span></div>' +
            '<div id="live-flash" aria-live="polite"></div><div id="live-score-body" class="mt-2"></div><div id="live-editor"></div>' +
            '</div></section>';
        document.getElementById('live-streams').addEventListener('click', function (e) {
            var b = e.target.closest('[data-choose-stream]');
            if (!b || !live) return;
            chosenStreamId = parseInt(b.dataset.chooseStream, 10);
            renderStreams(live);
        });
        try {
            show((await Api.get('/api/matches/' + matchId + '/live')).data, true);
        } catch (err) {
            root.innerHTML = ''; // trang trận đã tự báo lỗi "Không tìm thấy trận đấu"
        }
    });
})();
