var currentTournamentId = null;

function flashBox() { return document.getElementById('flash'); }

function showError(message) {
    Ui.showAlert(flashBox(), 'danger', message);
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

// Số nguyên trong khoảng [min, max]; trả về null nếu sai.
function readInt(id, min, max) {
    var raw = (document.getElementById(id).value || '').trim();
    if (!/^\d+$/.test(raw)) return null;
    var n = parseInt(raw, 10);
    return n >= min && n <= max ? n : null;
}

// Giá trị ô datetime-local -> ISO, hoặc null nếu bỏ trống/sai.
function readDate(id) {
    var raw = document.getElementById(id).value;
    if (!raw) return null;
    var d = new Date(raw);
    return isNaN(d.getTime()) ? null : d;
}

// Nạp danh sách Tựa Game trực tiếp từ DB vào Dropdown Select
async function loadGameSelect() {
    var gameSelect = document.getElementById('tGameId');
    if (!gameSelect) return;
    try {
        var res = await Api.get('/api/teams/games');
        var games = Array.isArray(res) ? res : (res && res.data ? res.data : []);
        gameSelect.innerHTML = games.length > 0
            ? '<option value="">-- Chọn tựa game --</option>' + games.map(function (g) {
                // Kèm luật đội hình của game để BTC biết mỗi đội cần bao nhiêu người.
                var rule = g.maxStarters ? ' · ' + g.maxStarters + ' chính thức' + (g.maxSubstitutes ? ', dự bị không bắt buộc (tối đa ' + g.maxSubstitutes + ')' : '') : '';
                return '<option value="' + g.gameId + '">' + Ui.escapeHtml(g.title + rule) + '</option>';
            }).join('')
            : '<option value="">-- Chưa có tựa game khả dụng --</option>';
    } catch (e) {
        gameSelect.innerHTML = '<option value="">-- Không thể tải danh sách game --</option>';
    }
}

// Đơn vị tổ chức gắn với tài khoản: tên tài khoản (mặc định) hoặc đơn vị tổ chức đã duyệt mà tài khoản phụ trách.
async function loadOrganizerUnit() {
    var select = document.getElementById('tOrganizerUnit');
    if (!select) return;
    // Ban tổ chức là một tổ chức: giải luôn đứng tên một đơn vị đã duyệt mà bạn là Chủ nhiệm / Điều hành giải.
    var submit = document.querySelector('#step1Form button[type="submit"]');
    var hint = document.getElementById('tOrganizerUnitHint');
    try {
        var who = await Api.get('/api/TournamentSetup/organizer-identity');
        var orgs = (who && who.organizations) || [];
        if (!orgs.length) {
            select.innerHTML = '<option value="">Chưa thuộc đơn vị nào</option>';
            select.disabled = true;
            if (submit) submit.disabled = true;
            hint.innerHTML = 'Bạn cần thuộc một đơn vị tổ chức đã được duyệt (Chủ nhiệm hoặc Điều hành giải) mới tạo được giải. ' +
                '<a href="/organization/index.html?mine=1">Lập đơn vị hoặc xem lời mời</a>.';
            return;
        }
        select.innerHTML = orgs.map(function (o) { return '<option value="' + Number(o.organizationId) + '">' + Ui.escapeHtml(o.name) + '</option>'; }).join('');
        select.disabled = orgs.length === 1;   // chỉ một đơn vị thì khóa cho rõ là không đổi được
        hint.textContent = 'Giải đứng tên đơn vị tổ chức bạn đang là Chủ nhiệm hoặc Điều hành giải.';
    } catch (e) {
        select.innerHTML = '<option value="">Không tải được đơn vị</option>';
        select.disabled = true;
        if (submit) submit.disabled = true;
    }
}

document.addEventListener('DOMContentLoaded', async function () {
    var user = await Ui.requireAuth(['ORGANIZER']);
    if (!user) return;

    Ui.consumeFlash(flashBox());
    wireStepTabs();
    await Promise.all([loadGameSelect(), loadOrganizerUnit()]);

    // Ẩn/hiện khối cấu hình Hybrid (TOUR-04) khi chọn Thể thức; vòng tròn không có trận tranh hạng Ba.
    var formatSelect = document.getElementById('tFormat');
    var hybridBlock = document.getElementById('hybridConfigBlock');
    var thirdPlace = document.getElementById('chkThirdPlace');
    function syncFormat() {
        hybridBlock.style.display = formatSelect.value === 'HYBRID' ? 'block' : 'none';
        // Tranh hạng Ba chỉ có ở loại trực tiếp / vòng bảng rồi loại trực tiếp (loại kép đã có nhánh thua).
        thirdPlace.disabled = !(formatSelect.value === 'SINGLE_ELIMINATION' || formatSelect.value === 'HYBRID');
        if (thirdPlace.disabled) thirdPlace.checked = false;
    }
    formatSelect.addEventListener('change', syncFormat);
    syncFormat();

    // Ảnh bìa (mục 22): chọn ngay ở bước 1, tải lên khi bước 1 lưu xong (lúc đó mới có mã giải).
    var banner = BannerPicker.mount(document.getElementById('bannerRoot'), {
        tournamentId: function () { return currentTournamentId; },
        current: null
    });

    // TOUR-01, TOUR-03, TOUR-04: Bước 1 - Thông tin & Thể thức
    var step1Form = document.getElementById('step1Form');
    step1Form.addEventListener('submit', async function (e) {
        e.preventDefault();
        Ui.clearFieldErrors(step1Form);
        Ui.clearAlert(flashBox());

        var name = document.getElementById('tName').value.trim();
        var gameId = parseInt(document.getElementById('tGameId').value, 10);
        var selectedFormat = formatSelect.value;
        if (name.length < 3 || name.length > 200) return showError('Tên giải đấu phải từ 3 đến 200 ký tự.');
        if (!gameId) return showError('Vui lòng chọn tựa game thi đấu.');

        var groupCount = null, advancing = null;
        if (selectedFormat === 'HYBRID') {
            groupCount = readInt('tGroupCount', 2, 16);
            advancing = readInt('tAdvancingPerGroup', 1, 8);
            if (groupCount === null) return showError('Số bảng đấu phải là số nguyên từ 2 đến 16.');
            if (advancing === null) return showError('Số đội đi tiếp mỗi bảng phải là số nguyên từ 1 đến 8.');
        }

        var payload = {
            name: name,
            description: document.getElementById('tDesc').value,
            gameId: gameId,
            organizationId: parseInt(document.getElementById('tOrganizerUnit').value, 10) || null,
            location: '',
            format: selectedFormat,
            hasThirdPlaceMatch: thirdPlace.checked,
            groupCount: groupCount,
            advancingPerGroup: advancing
        };

        try {
            // Đã có bản nháp (người dùng quay lại bước 1) -> SỬA bản nháp đó, không tạo thêm giải mới.
            if (currentTournamentId) {
                await Api.put('/api/TournamentSetup/' + currentTournamentId + '/step1', payload);
                document.getElementById('lblTournamentId').textContent = name;
                markSaved(1); switchStep(2);
                Ui.showAlert(flashBox(), 'success', 'Đã cập nhật thông tin giải. Kiểm tra lại thời gian và số đội.');
                return;
            }
            var res = await Api.post('/api/TournamentSetup/step1', payload);
            currentTournamentId = res.tournamentId || res;
            document.getElementById('lblTournamentId').textContent = name;
            await banner.flush();
            markSaved(1); switchStep(2);
            Ui.showAlert(flashBox(), 'success', 'Đã khởi tạo bản nháp giải đấu. Tiếp tục cài đặt thời gian và số đội.');
        } catch (err) {
            Ui.applyFieldErrors(step1Form, err.fieldErrors);
            showError(err.message);
        }
    });

    // TOUR-02: Bước 2 - Timeline & Quy mô
    var step2Form = document.getElementById('step2Form');
    step2Form.addEventListener('submit', async function (e) {
        e.preventDefault();
        Ui.clearAlert(flashBox());
        if (!currentTournamentId) return showError('Chưa có mã giải đấu — hãy hoàn thành bước 1 trước.');

        var open = readDate('tRegOpen'), close = readDate('tRegClose'), start = readDate('tStart'), end = readDate('tEnd');
        if (!open || !close || !start || !end) return showError('Vui lòng nhập đủ 4 mốc thời gian.');
        // Cùng luật với máy chủ (Utils/TournamentTimeline.cs).
        var HOUR = 3600 * 1000;
        if (close <= new Date()) return showError('Thời gian đóng đăng ký phải ở tương lai.');
        if (open >= close) return showError('Thời gian đóng đăng ký phải sau thời gian mở đăng ký.');
        if (close - open < 24 * HOUR) return showError('Thời gian nhận đăng ký phải kéo dài ít nhất 1 ngày (đóng đăng ký sau mở đăng ký từ 24 giờ trở lên).');
        if (close > start) return showError('Ngày khai mạc phải sau thời gian đóng đăng ký.');
        if (start - close < HOUR) return showError('Khai mạc phải sau đóng đăng ký ít nhất 1 giờ để Ban tổ chức duyệt đơn và sinh nhánh đấu.');
        if (end <= start) return showError('Ngày kết thúc phải sau ngày khai mạc.');
        if (end - start < HOUR) return showError('Giải phải kéo dài ít nhất 1 giờ tính từ khai mạc.');

        var maxTeams = readInt('tMaxTeams', 4, 128);
        if (maxTeams === null || (maxTeams & (maxTeams - 1)) !== 0)
            return showError('Số đội tối đa phải là lũy thừa của 2, từ 4 đến 128 (4, 8, 16, 32, 64, 128).');

        try {
            await Api.put('/api/TournamentSetup/' + currentTournamentId + '/step2-timeline', {
                registrationOpenDate: open.toISOString(),
                registrationCloseDate: close.toISOString(),
                tournamentStartDate: start.toISOString(),
                tournamentEndDate: end.toISOString(),
                maxTeams: maxTeams
            });
            markSaved(2); switchStep(3);
        } catch (err) {
            showError(err.message);
        }
    });

    // TOUR-06: Bước 4 - Tài chính
    var step4Form = document.getElementById('step4Form');
    var feeInput = document.getElementById('tEntryFee');
    var bankSelect = document.getElementById('tBankName');
    var numberInput = document.getElementById('tAccountNumber');
    var holderInput = document.getElementById('tAccountName');
    BankAccount.fillSelect(bankSelect);
    BankAccount.wireInputs(numberInput, holderInput);
    // Có lệ phí -> hiện dấu * bắt buộc ở khối tài khoản.
    function syncFeeRequired() {
        document.querySelector('[data-fee-req]').hidden = !(Number(feeInput.value) > 0);
    }
    feeInput.addEventListener('input', syncFeeRequired);
    syncFeeRequired();

    step4Form.addEventListener('submit', async function (e) {
        e.preventDefault();
        Ui.clearAlert(flashBox());
        Ui.clearFieldErrors(step4Form);
        if (!currentTournamentId) return;

        var feeRaw = (feeInput.value || '0').trim();
        if (!/^\d+$/.test(feeRaw) || Number(feeRaw) > 100000000)
            return showError('Lệ phí tham gia phải là số nguyên từ 0 đến 100.000.000 VNĐ.');
        var fee = Number(feeRaw);
        holderInput.value = BankAccount.normalizeHolder(holderInput.value);
        var bad = BankAccount.validate(fee, bankSelect.value, numberInput.value, holderInput.value);
        if (bad) {
            var errs = {}; errs[bad.field] = bad.message;
            Ui.applyFieldErrors(step4Form, errs);
            return showError(bad.message);
        }

        try {
            await Api.put('/api/TournamentSetup/' + currentTournamentId + '/step4-finance', {
                entryFee: fee,
                bankName: bankSelect.value,
                accountNumber: numberInput.value.trim(),
                accountName: holderInput.value,
                prizePoolDescription: document.getElementById('tPrize').value
            });
            markSaved(4);
            switchStep(5);
        } catch (err) {
            showError(err.message);
        }
    });

    // Bước 5 - Thể lệ & bộ luật (soạn trên web / tải PDF / chọn bộ luật) — bỏ qua được, sửa sau ở Sửa thông tin.
    document.getElementById('saveStep5').addEventListener('click', async function () {
        if (!currentTournamentId || !rulesEditor) { markSaved(5); return switchStep(6); }
        var btn = this;
        btn.disabled = true;
        try {
            var r = await rulesEditor.save();
            if (r.messages.length) Ui.showAlert(flashBox(), 'success', r.messages.join(' '));
            markSaved(5); switchStep(6);
        } catch (err) {
            showError(err.message || 'Không lưu được thể lệ.');
        } finally {
            btn.disabled = false;
        }
    });
    document.getElementById('skipStep5').addEventListener('click', function () { markSaved(5); switchStep(6); });

    // Nút "← Quay lại" ở mỗi bước: về bước trước, giữ nguyên dữ liệu đã nhập (lưu lại sẽ cập nhật bản nháp).
    document.querySelectorAll('[data-back]').forEach(function (b) {
        b.addEventListener('click', function () { Ui.clearAlert(flashBox()); switchStep(parseInt(b.dataset.back, 10)); });
    });

    // TOUR-07: Bước 6 - Yêu cầu hồ sơ & Công bố
    var step5Form = document.getElementById('step5Form');
    step5Form.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (!currentTournamentId) return;

        try {
            await Api.post('/api/TournamentSetup/' + currentTournamentId + '/publish', {
                requireStudentCard: document.getElementById('chkStudentCard').checked,
                requirePaymentBill: document.getElementById('chkPaymentProof').checked,
                requireIgnVerify: document.getElementById('chkIgn').checked
            });
            Ui.setFlash('Đã công bố giải đấu. Giải mới nằm trong danh sách "Giải đấu của tôi".', 'success');
            location.href = '/registration/organizer.html';
        } catch (err) {
            showError(err.message);
        }
    });
});

// TOUR-05: Bước 3 - Cấu hình Best-of
var BO_CHOICES = [1, 3, 5, 7];
var step3Rounds = [];

// Bước 3: lấy các vòng knockout thật của giải (theo thể thức + số đội đã lưu) rồi hiện ô chọn BO cho từng vòng.
async function loadStep3() {
    var box = document.getElementById('boRounds');
    if (!box || !currentTournamentId) return;
    box.innerHTML = '<div class="hint">Đang tải các vòng…</div>';
    step3Rounds = [];
    try {
        var board = await Api.get('/api/TournamentSetup/' + currentTournamentId + '/step3-bo-config');
        step3Rounds = Array.isArray(board.rounds) ? board.rounds : [];
        var note = board.note ? '<div class="hint mb-2">' + Ui.escapeHtml(board.note) + '</div>' : '';
        if (!step3Rounds.length) { box.innerHTML = note || '<div class="hint">Giải không có vòng loại trực tiếp.</div>'; return; }
        box.innerHTML = note + '<div class="form-grid">' + step3Rounds.map(function (r) {
            var id = 'bo-round-' + Number(r.roundNo);
            return '<div class="field"><label for="' + id + '">' + Ui.escapeHtml(r.label) + ' <span class="req">*</span></label>' +
                '<select class="select" id="' + id + '" data-round="' + Number(r.roundNo) + '" required>' +
                BO_CHOICES.map(function (bo) {
                    return '<option value="' + bo + '"' + (bo === r.bestOf ? ' selected' : '') + '>BO' + bo + ' — thắng ' + ((bo + 1) / 2) + ' ván</option>';
                }).join('') + '</select></div>';
        }).join('') + '</div>' +
        (board.knockoutTeams ? '<div class="hint">Nhánh loại trực tiếp có ' + Number(board.knockoutTeams) + ' suất.</div>' : '');
    } catch (err) {
        box.innerHTML = '<div class="hint">Không tải được danh sách vòng.</div>';
        showError(err.message);
    }
}

async function submitStep3() {
    if (!currentTournamentId) return;
    var configs = [];
    var invalid = false;
    document.querySelectorAll('#boRounds select[data-round]').forEach(function (sel) {
        var bo = parseInt(sel.value, 10);
        var round = parseInt(sel.dataset.round, 10);
        var ok = BO_CHOICES.indexOf(bo) >= 0 && round >= 1;
        sel.classList.toggle('is-invalid', !ok);
        if (!ok) invalid = true;
        configs.push({ stageOrder: round, roundNo: round, roundName: '', bestOf: bo });
    });
    if (invalid) { showError('Best of chỉ được là 1, 3, 5 hoặc 7.'); return; }
    if (step3Rounds.length && configs.length !== step3Rounds.length) { showError('Hãy chọn Best of cho đủ tất cả các vòng.'); return; }
    var payload = { roundConfigs: configs };

    try {
        await Api.put('/api/TournamentSetup/' + currentTournamentId + '/step3-bo-config', payload);
        markSaved(3); switchStep(4);
    } catch (err) {
        showError(err.message);
    }
}

// Chuyển tab trong Wizard
// Bước 5: khối Thể lệ & bộ luật (js/rules-editor.js), dựng một lần khi lần đầu vào bước 5.
var rulesEditor = null;
async function loadStep5() {
    if (rulesEditor || !currentTournamentId) return;
    try { rulesEditor = await RulesEditor.mount(document.getElementById('rulesRoot'), currentTournamentId); }
    catch (e) { document.getElementById('rulesRoot').innerHTML = '<div class="hint">Không tải được phần thể lệ — có thể bỏ qua và bổ sung sau.</div>'; }
}

// Thanh bước bấm được: về bất kỳ bước đã qua, hoặc tiến tới bước đã từng tới (maxReached).
// Sửa bước 1–2 (game, thể thức, số đội) làm đổi các vòng đấu nên phải đi lại từ bước kế tiếp.
var currentStep = 1, maxReached = 1, dirtySteps = {};
function markSaved(step) {
    var changed = dirtySteps[step] !== false;   // lần lưu đầu, hoặc có sửa từ lần lưu trước
    dirtySteps[step] = false;
    maxReached = step <= 2 && changed ? step + 1 : Math.max(maxReached, step + 1);
}
function goToStep(n) {
    if (!n || n === currentStep || n > maxReached) return;
    if (n > currentStep && dirtySteps[currentStep])
        return showError('Bước ' + currentStep + ' có thay đổi chưa lưu — bấm nút "Lưu" ở cuối bước trước khi sang bước sau.');
    Ui.clearAlert(flashBox());
    switchStep(n);
}
function wireStepTabs() {
    document.querySelectorAll('.wizard-steps [data-step]').forEach(function (tab) {
        var n = parseInt(tab.dataset.step, 10);
        tab.addEventListener('click', function () { goToStep(n); });
        tab.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); goToStep(n); }
        });
    });
    // Gõ/chọn gì trong một bước thì bước đó "chưa lưu" cho tới khi bấm Lưu.
    for (var i = 1; i <= 6; i++) (function (step) {
        var panel = document.getElementById('panelStep' + step);
        if (!panel) return;
        ['input', 'change'].forEach(function (ev) { panel.addEventListener(ev, function () { dirtySteps[step] = true; }); });
    })(i);
}

function switchStep(stepNum) {
    currentStep = stepNum;
    if (stepNum > maxReached) maxReached = stepNum;
    for (var i = 1; i <= 6; i++) {
        var panel = document.getElementById('panelStep' + i);
        var tab = document.getElementById('tabStep' + i);
        if (panel) panel.style.display = (i === stepNum) ? 'block' : 'none';
        if (tab) {
            // Giao diện bước nằm trong site.css: .step-tab.active / .done / .reachable
            var reachable = i <= maxReached && i !== stepNum;
            tab.classList.toggle('active', i === stepNum);
            tab.classList.toggle('done', i < stepNum || (reachable && i < maxReached));
            tab.classList.toggle('reachable', reachable);
            if (reachable) { tab.setAttribute('role', 'button'); tab.tabIndex = 0; tab.title = 'Về bước ' + i; }
            else { tab.removeAttribute('role'); tab.removeAttribute('tabindex'); tab.removeAttribute('title'); }
            if (i === stepNum) tab.setAttribute('aria-current', 'step'); else tab.removeAttribute('aria-current');
        }
    }
    window.scrollTo({ top: 0, behavior: 'smooth' });
    if (stepNum === 3) loadStep3();
    if (stepNum === 5) loadStep5();
}
