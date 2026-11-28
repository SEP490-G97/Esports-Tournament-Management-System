(function () {
    var tournamentId = null;
    var selected = null; // Trọng tài đang chọn: { accountId, fullName }
    var searchTimer = null;

    function base() { return '/api/tournaments/' + tournamentId; }

    function showEmpty(title, message) {
        document.getElementById('referee-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    // Server trả giờ UTC không kèm múi giờ.
    function formatTime(value) {
        if (!value) return 'Chưa xếp lịch';
        var date = new Date(/Z$/.test(value) ? value : value + 'Z');
        return date.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    function pickerHtml(board) {
        var rounds = board.rounds.map(function (r) {
            return '<option value="' + r.stageNo + ':' + r.roundNo + '">' + Ui.escapeHtml(r.label) + '</option>';
        }).join('');

        return '<div class="card-etoms mb-3"><div class="body">' +
            '<div class="field"><label for="referee-search">Chọn trọng tài</label>' +
            '<input id="referee-search" maxlength="100" class="input" autocomplete="off" placeholder="Nhập tên hoặc email, ít nhất 2 ký tự" />' +
            '<div class="hint" id="selected-referee">Chưa chọn trọng tài.</div>' +
            '<div id="candidate-list" style="margin-top:10px"></div></div>' +
            (rounds
                ? '<div class="field mb-0"><label for="round-select">Gán cả vòng</label>' +
                  '<div class="d-flex flex-wrap gap-2"><select id="round-select" class="select filter-select">' + rounds + '</select>' +
                  '<button type="button" class="btn btn-ghost" id="assign-round">Gán cho cả vòng</button></div></div>'
                : '') +
            '</div></div>';
    }

    function matchRow(m) {
        var referee = m.refereeName
            ? 'Trọng tài: ' + m.refereeName
            : 'Chưa có trọng tài, Ban tổ chức phân xử';
        var actions = m.canAssign
            ? '<button type="button" class="btn btn-primary" data-assign="' + m.matchId + '">Gán</button>' +
              (m.refereeAccountId ? '<button type="button" class="btn btn-ghost" data-clear="' + m.matchId + '">Bỏ</button>' : '')
            : '';

        return '<div class="member-card" style="justify-content:space-between;flex-wrap:wrap;gap:10px;margin-bottom:10px">' +
            '<div class="grow"><div class="nm">' + Ui.escapeHtml(m.teamAName + ' vs ' + m.teamBName) + '</div>' +
            '<div class="rl">' + Ui.escapeHtml(m.roundLabel + ' · ' + formatTime(m.scheduledAt)) + '</div>' +
            '<div class="rl">' + Ui.escapeHtml(referee) + '</div></div>' +
            '<span class="badge ' + Ui.escapeHtml(m.statusClass) + '">' + Ui.escapeHtml(m.statusLabel) + '</span>' +
            '<div class="d-flex gap-2">' + actions +
            '<a class="btn btn-ghost" href="/match/detail.html?id=' + m.matchId + '">Mở trận</a>' +
            '<a class="btn btn-ghost" href="/match/pick-ban.html?id=' + m.matchId + '">Cấm chọn bản đồ</a></div></div>';
    }

    function render(board) {
        document.getElementById('page-title').textContent = 'Gán trọng tài ' + board.tournamentName;

        var blocked = board.blockedReason
            ? '<div class="pending-banner">' + Ui.escapeHtml(board.blockedReason) + '</div>'
            : '';
        var list = board.matches.length
            ? '<div data-ref-matches></div>'
            : '<div class="card-etoms"><div class="empty"><h3>Giải chưa có trận đấu</h3><p>Trận đấu được tạo khi sinh nhánh đấu.</p></div></div>';

        var root = document.getElementById('referee-root');
        root.innerHTML =
            blocked + (board.canAssign ? pickerHtml(board) : '') + list +
            '<div class="btn-row mt-4">' +
            '<a class="btn btn-ghost" href="/tournament/dashboard.html?id=' + tournamentId + '">Bảng điều hành</a></div>';

        selected = null;
        if (board.canAssign) wirePicker();
        // Giải nhiều trận: tìm theo đội/vòng/trọng tài + phân trang 10 trận (góp ý 27/11); nút Gán/Bỏ gắn lại cho trang vừa vẽ.
        if (board.matches.length) Ui.listPager(root.querySelector('[data-ref-matches]'), {
            items: board.matches, pageSize: 10, listClass: 'ref-match-list', render: matchRow,
            placeholder: 'Tìm trận theo đội, vòng, trọng tài...',
            text: function (m) { return m.teamAName + ' ' + m.teamBName + ' ' + m.roundLabel + ' ' + (m.refereeName || 'chưa có trọng tài') + ' ' + m.statusLabel; },
            after: wireMatchButtons
        });
    }

    function selectReferee(candidate) {
        selected = candidate;
        document.getElementById('selected-referee').textContent = 'Đang chọn: ' + candidate.fullName + ' (' + candidate.email + ')';
        document.getElementById('candidate-list').innerHTML = '';
    }

    function renderCandidates(candidates, q) {
        var box = document.getElementById('candidate-list');
        if (!candidates.length) {
            box.innerHTML = q
                ? '<div class="hint">Không tìm thấy trọng tài phù hợp.</div>'
                : '<div class="hint">Đơn vị đứng tên giải chưa có thành viên vai trò Trọng tài. Chủ nhiệm đơn vị mời tài khoản Ban tổ chức vào đơn vị rồi đổi vai trò thành "Trọng tài" ở trang Quản lý đơn vị.</div>';
            return;
        }
        box.innerHTML = '';
        candidates.forEach(function (c) {
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn btn-ghost';
            btn.style.margin = '0 8px 8px 0';
            btn.textContent = c.fullName + ' · ' + c.email;
            btn.addEventListener('click', function () { selectReferee(c); });
            box.appendChild(btn);
        });
    }

    // Giải đứng tên đơn vị: danh sách = các Trọng tài của đơn vị (hiện ngay, gõ để lọc).
    async function loadCandidates(q) {
        try {
            renderCandidates(await Api.get(base() + '/referee-candidates?q=' + encodeURIComponent(q || '')), q);
        } catch (err) {
            Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
        }
    }

    function wirePicker() {
        var input = document.getElementById('referee-search');
        input.addEventListener('input', function () {
            clearTimeout(searchTimer);
            var q = input.value.trim();
            searchTimer = setTimeout(function () { loadCandidates(q); }, 250);
        });
        loadCandidates('');

        var roundBtn = document.getElementById('assign-round');
        if (roundBtn) roundBtn.addEventListener('click', assignRound);
    }

    function requireSelected() {
        if (selected) return true;
        Ui.showAlert(document.getElementById('flash'), 'danger', 'Vui lòng chọn trọng tài trước.');
        return false;
    }

    async function run(action) {
        var flash = document.getElementById('flash');
        Ui.clearAlert(flash);
        try {
            var res = await action();
            await load();
            return res;
        } catch (err) {
            Ui.showAlert(flash, 'danger', err.message);
            return null;
        }
    }

    async function assignRound() {
        if (!requireSelected()) return;
        var parts = document.getElementById('round-select').value.split(':');
        var res = await run(function () {
            return Api.post(base() + '/referee-board/round', {
                stageNo: parseInt(parts[0], 10),
                roundNo: parseInt(parts[1], 10),
                refereeAccountId: selected.accountId
            });
        });
        if (!res) return;
        var skipped = res.data.skippedMatches;
        Ui.showAlert(document.getElementById('flash'), skipped.length ? 'danger' : 'success', res.message + (skipped.length ? ' ' + skipped.join(' ') : ''));
    }

    function wireMatchButtons(scope) {
        scope = scope || document;
        scope.querySelectorAll('[data-assign]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                if (!requireSelected()) return;
                var refereeId = selected.accountId;
                var res = await run(function () {
                    return Api.put(base() + '/matches/' + btn.dataset.assign + '/referee', { refereeAccountId: refereeId });
                });
                if (res) Ui.showAlert(document.getElementById('flash'), 'success', res.message);
            });
        });

        scope.querySelectorAll('[data-clear]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                if (!await window.etomsConfirm('Bỏ trọng tài của trận này? Quyền phân xử sẽ thuộc Ban tổ chức.')) return;
                var res = await run(function () {
                    return Api.put(base() + '/matches/' + btn.dataset.clear + '/referee', { refereeAccountId: null });
                });
                if (res) Ui.showAlert(document.getElementById('flash'), 'success', res.message);
            });
        });
    }

    async function load() {
        try {
            render(await Api.get(base() + '/referee-board'));
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể gán trọng tài cho giải này', 'Chỉ Ban tổ chức của giải được gán trọng tài.');
            else showEmpty('Không tải được danh sách trận', err.message);
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
        await load();
    });
})();
