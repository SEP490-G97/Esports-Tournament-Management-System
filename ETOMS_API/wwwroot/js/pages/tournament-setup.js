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
                return '<option value="' + g.gameId + '">' + Ui.escapeHtml(g.title) + '</option>';
            }).join('')
            : '<option value="">-- Chưa có tựa game khả dụng --</option>';
    } catch (e) {
        gameSelect.innerHTML = '<option value="">-- Không thể tải danh sách game --</option>';
    }
}

document.addEventListener('DOMContentLoaded', async function () {
    var user = await Ui.requireAuth(['ORGANIZER']);
    if (!user) return;

    Ui.consumeFlash(flashBox());
    await loadGameSelect();

    // Ẩn/hiện khối cấu hình Hybrid (TOUR-04) khi chọn Thể thức; vòng tròn không có trận tranh hạng Ba.
    var formatSelect = document.getElementById('tFormat');
    var hybridBlock = document.getElementById('hybridConfigBlock');
    var thirdPlace = document.getElementById('chkThirdPlace');
    function syncFormat() {
        hybridBlock.style.display = formatSelect.value === 'HYBRID' ? 'block' : 'none';
        thirdPlace.disabled = formatSelect.value === 'ROUND_ROBIN';
        if (thirdPlace.disabled) thirdPlace.checked = false;
    }
    formatSelect.addEventListener('change', syncFormat);
    syncFormat();

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
            organizerName: document.getElementById('tOrganizerName').value,
            location: '',
            format: selectedFormat,
            hasThirdPlaceMatch: thirdPlace.checked,
            groupCount: groupCount,
            advancingPerGroup: advancing
        };

        try {
            var res = await Api.post('/api/TournamentSetup/step1', payload);
            currentTournamentId = res.tournamentId || res;
            document.getElementById('lblTournamentId').textContent = currentTournamentId;
            switchStep(2);
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
        if (close <= new Date()) return showError('Thời gian đóng đăng ký phải ở tương lai.');
        if (open >= close) return showError('Thời gian mở đăng ký phải trước thời gian đóng đăng ký.');
        if (close > start) return showError('Thời gian đóng đăng ký phải trước hoặc bằng ngày bắt đầu thi đấu.');
        if (start >= end) return showError('Ngày bắt đầu thi đấu phải trước ngày kết thúc.');

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
            switchStep(3);
        } catch (err) {
            showError(err.message);
        }
    });

    // TOUR-06: Bước 4 - Tài chính
    var step4Form = document.getElementById('step4Form');
    step4Form.addEventListener('submit', async function (e) {
        e.preventDefault();
        Ui.clearAlert(flashBox());
        if (!currentTournamentId) return;

        var feeRaw = (document.getElementById('tEntryFee').value || '0').trim();
        if (!/^\d+$/.test(feeRaw) || Number(feeRaw) > 100000000)
            return showError('Lệ phí tham gia phải là số nguyên từ 0 đến 100.000.000 VNĐ.');
        var fee = Number(feeRaw);
        var bank = document.getElementById('tBankName').value.trim();
        var number = document.getElementById('tAccountNumber').value.trim();
        var holder = document.getElementById('tAccountName').value.trim();
        if (fee > 0 && (!bank || !number || !holder))
            return showError('Giải có lệ phí phải nhập đủ ngân hàng, số tài khoản và chủ tài khoản nhận phí.');
        if (number && !/^\d{6,20}$/.test(number)) return showError('Số tài khoản chỉ gồm chữ số, dài 6–20 số.');

        try {
            await Api.put('/api/TournamentSetup/' + currentTournamentId + '/step4-finance', {
                entryFee: fee,
                bankName: bank,
                accountNumber: number,
                accountName: holder,
                prizePoolDescription: document.getElementById('tPrize').value
            });
            switchStep(5);
        } catch (err) {
            showError(err.message);
        }
    });

    // TOUR-07: Bước 5 - Yêu cầu hồ sơ & Công bố
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
async function submitStep3() {
    if (!currentTournamentId) return;
    var payload = {
        roundConfigs: [
            { stageOrder: 1, roundName: "Tứ kết", bestOf: 1 },
            { stageOrder: 2, roundName: "Bán kết", bestOf: 3 },
            { stageOrder: 3, roundName: "Chung kết", bestOf: 5 }
        ]
    };

    try {
        await Api.put('/api/TournamentSetup/' + currentTournamentId + '/step3-bo-config', payload);
        switchStep(4);
    } catch (err) {
        showError(err.message);
    }
}

// Chuyển tab trong Wizard
function switchStep(stepNum) {
    for (var i = 1; i <= 5; i++) {
        var panel = document.getElementById('panelStep' + i);
        var tab = document.getElementById('tabStep' + i);
        if (panel) panel.style.display = (i === stepNum) ? 'block' : 'none';
        if (tab) {
            // Giao diện bước (màu, đánh dấu đã xong) nằm trong site.css: .step-tab.active / .step-tab.done
            tab.classList.toggle('active', i === stepNum);
            tab.classList.toggle('done', i < stepNum);
            if (i === stepNum) tab.setAttribute('aria-current', 'step'); else tab.removeAttribute('aria-current');
        }
    }
    window.scrollTo({ top: 0, behavior: 'smooth' });
}
