(function () {
    var tournamentSelect = document.getElementById('marketing-tournament');
    var affiliateSelect = document.getElementById('qr-affiliate-code');
    var flash = document.getElementById('marketing-flash');
    var links = [];

    function showFailure(error) {
        Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể thực hiện thao tác.');
    }

    function selectedTournamentId() {
        return Number(tournamentSelect.value);
    }

    function codeQuery(code) {
        return code ? '?code=' + encodeURIComponent(code) : '';
    }

    function renderLinks() {
        var body = document.getElementById('affiliate-list');
        var options = ['<option value="">Link của ban tổ chức</option>'];
        links.forEach(function (link) {
            options.push('<option value="' + Ui.escapeHtml(link.code) + '">' +
                Ui.escapeHtml(link.label) + ' · ' + Ui.escapeHtml(link.code) + '</option>');
        });
        affiliateSelect.innerHTML = options.join('');
        body.innerHTML = links.length ? links.map(function (link) {
            return '<tr><td>' + Ui.escapeHtml(link.label) + '<br><small>' + Ui.escapeHtml(link.code) + '</small></td>' +
                '<td>' + link.visitCount + '</td><td>' + link.successfulRegistrations + '</td>' +
                '<td><button class="btn btn-ghost btn-sm copy-link" type="button" data-url="' +
                Ui.escapeHtml(link.shareUrl) + '">Sao chép</button></td></tr>';
        }).join('') : '<tr><td colspan="4">Chưa có liên kết cộng tác viên.</td></tr>';
        body.querySelectorAll('.copy-link').forEach(function (button) {
            button.addEventListener('click', async function () {
                try {
                    await navigator.clipboard.writeText(button.dataset.url);
                    Ui.showAlert(flash, 'success', 'Đã sao chép liên kết.');
                } catch (_) {
                    window.prompt('Sao chép liên kết này:', button.dataset.url);
                }
            });
        });
    }

    function updateAssetLinks() {
        var tournamentId = selectedTournamentId();
        var code = affiliateSelect.value;
        var query = codeQuery(code);
        var qr = document.getElementById('marketing-qr');
        var qrUrl = '/api/marketing/tournaments/' + tournamentId + '/qr.svg' + query;
        qr.src = qrUrl;
        qr.hidden = false;
        var download = document.getElementById('download-qr');
        download.href = qrUrl;
        download.download = 'etoms-qr-' + tournamentId + '.svg';
        download.hidden = false;
        ['square', 'story', 'landscape', 'logo', 'avatar-frame'].forEach(function (format) {
            var link = document.getElementById('asset-' + format);
            link.href = '/api/marketing/tournaments/' + tournamentId + '/materials/' + format + '.svg' + query;
        });
    }

    async function loadLinks() {
        var tournamentId = selectedTournamentId();
        if (!tournamentId) return;
        try {
            var response = await Api.get('/api/marketing/tournaments/' + tournamentId + '/affiliate-links');
            links = response.data || [];
            renderLinks();
            updateAssetLinks();
        } catch (error) {
            showFailure(error);
            links = [];
            renderLinks();
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['ORGANIZER']);
        if (!me) return;

        try {
            var response = await Api.get('/api/marketing/mine/tournaments');
            var tournaments = response.data || [];
            tournamentSelect.innerHTML = tournaments.length
                ? tournaments.map(function (item) {
                    return '<option value="' + item.tournamentId + '">' +
                        Ui.escapeHtml(item.name) + ' · ' + Ui.escapeHtml(item.status) + '</option>';
                }).join('')
                : '<option value="">Tài khoản hiện không quản lý giải nào</option>';
            if (!tournaments.length) return;
            await loadLinks();
        } catch (error) {
            showFailure(error);
            tournamentSelect.innerHTML = '<option value="">Không thể tải giải đấu</option>';
        }
    });

    tournamentSelect.addEventListener('change', loadLinks);
    affiliateSelect.addEventListener('change', updateAssetLinks);
    document.getElementById('affiliate-form').addEventListener('submit', async function (event) {
        event.preventDefault();
        var label = document.getElementById('affiliate-label').value.trim();
        if (!label || !selectedTournamentId()) return;
        try {
            await Api.post('/api/marketing/tournaments/' + selectedTournamentId() + '/affiliate-links', { label: label });
            document.getElementById('affiliate-label').value = '';
            Ui.showAlert(flash, 'success', 'Đã tạo liên kết cộng tác viên.');
            await loadLinks();
        } catch (error) {
            showFailure(error);
        }
    });
})();
