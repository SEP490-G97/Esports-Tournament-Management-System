var currentTournamentId = null;

document.addEventListener('DOMContentLoaded', async function () {
    var user = await Ui.requireAuth();
    if (!user) return;

    Ui.consumeFlash(document.getElementById('flash'));

    // TOUR-01: Bước 1 - Thông tin chung
    var step1Form = document.getElementById('step1Form');
    step1Form.addEventListener('submit', async function (e) {
        e.preventDefault();
        Ui.clearFieldErrors(step1Form);

        var payload = {
            name: document.getElementById('tName').value,
            description: document.getElementById('tDesc').value,
            gameId: parseInt(document.getElementById('tGameId').value, 10),
            organizerName: document.getElementById('tOrganizerName').value,
            location: 'Hà Nội'
        };

        try {
            var res = await Api.post('/api/TournamentSetup/step1', payload);
            currentTournamentId = res.tournamentId || res;
            document.getElementById('lblTournamentId').textContent = currentTournamentId;

            switchStep(2);
            Ui.setFlash('Đã tạo thông tin cơ bản giải đấu!', 'success');
            Ui.consumeFlash(document.getElementById('flash'));
        } catch (err) {
            Ui.applyFieldErrors(step1Form, err.fieldErrors);
            Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
        }
    });

    // TOUR-02: Bước 2 - Timeline & Quy mô
    var step2Form = document.getElementById('step2Form');
    step2Form.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (!currentTournamentId) { alert('Chưa có mã giải đấu!'); return; }

        var payload = {
            registrationOpenDate: new Date(document.getElementById('tRegOpen').value).toISOString(),
            registrationCloseDate: new Date(document.getElementById('tRegClose').value).toISOString(),
            tournamentStartDate: new Date(document.getElementById('tStart').value).toISOString(),
            tournamentEndDate: new Date(document.getElementById('tEnd').value).toISOString(),
            maxTeams: parseInt(document.getElementById('tMaxTeams').value, 10)
        };

        try {
            await Api.put('/api/TournamentSetup/' + currentTournamentId + '/step2-timeline', payload);
            switchStep(3);
        } catch (err) {
            Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
        }
    });

    // TOUR-06: Bước 4 - Tài chính
    var step4Form = document.getElementById('step4Form');
    step4Form.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (!currentTournamentId) return;

        var payload = {
            entryFee: parseFloat(document.getElementById('tEntryFee').value || 0),
            bankName: document.getElementById('tBankName').value,
            accountNumber: document.getElementById('tAccountNumber').value,
            accountName: document.getElementById('tAccountName').value,
            prizePoolDescription: document.getElementById('tPrize').value
        };

        try {
            await Api.put('/api/TournamentSetup/' + currentTournamentId + '/step4-finance', payload);
            switchStep(5);
        } catch (err) {
            Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
        }
    });

    // TOUR-07: Bước 5 - Yêu cầu hồ sơ & Công bố
    var step5Form = document.getElementById('step5Form');
    step5Form.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (!currentTournamentId) return;

        var payload = {
            requireStudentCard: document.getElementById('chkStudentCard').checked,
            requirePaymentBill: document.getElementById('chkPaymentProof').checked,
            requireIgnVerify: document.getElementById('chkIgn').checked
        };

        try {
            await Api.post('/api/TournamentSetup/' + currentTournamentId + '/publish', payload);
            alert('🎉 Giải đấu đã được công bố thành công (PUBLISHED)!');
            location.href = '/tournament-setup/index.html';
        } catch (err) {
            Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
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
        Ui.showAlert(document.getElementById('flash'), 'danger', err.message);
    }
}

// Hàm hỗ trợ chuyển giao diện giữa các bước trong Wizard
function switchStep(stepNum) {
    for (var i = 1; i <= 5; i++) {
        var panel = document.getElementById('panelStep' + i);
        var tab = document.getElementById('tabStep' + i);
        if (panel) panel.style.display = (i === stepNum) ? 'block' : 'none';
        if (tab) {
            tab.style.color = (i === stepNum) ? 'var(--primary)' : 'var(--muted)';
            tab.style.fontWeight = (i === stepNum) ? 'bold' : 'normal';
        }
    }
    window.scrollTo({ top: 0, behavior: 'smooth' });
}