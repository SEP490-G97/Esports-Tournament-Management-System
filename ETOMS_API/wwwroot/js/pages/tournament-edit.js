(function () {
    var TEXT = ['Description', 'BannerUrl', 'OrganizingUnit', 'Region', 'ContactInfo', 'Venue', 'Name', 'BankAccountInfo', 'PrizeStructure', 'BestOfPerRound', 'Format'];
    var NUMBER = ['GameId', 'StarterCount', 'MaxSubstitutes', 'MaxTeams'];
    var OPTIONAL_NUMBER = ['GroupCount', 'AdvancePerGroup'];
    var CHECK = ['IsOnline', 'RequirePaymentProof', 'RequireInGameName', 'HasThirdPlaceMatch'];
    var DATES = ['RegOpenAt', 'RegCloseAt', 'StartAt', 'EndAt'];

    var tournamentId = null;
    var board = null;

    function flash() { return document.getElementById('flash'); }
    function el(id) { return document.getElementById(id); }
    function camel(name) { return name.charAt(0).toLowerCase() + name.slice(1); }

    function showEmpty(title, message) {
        el('edit-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
        el('edit-form').hidden = true;
    }

    // Server trả giờ UTC không kèm Z; ô datetime-local dùng giờ máy người dùng.
    function toLocalInput(value) {
        if (!value) return '';
        var d = new Date(/Z$/.test(value) ? value : value + 'Z');
        var pad = function (n) { return String(n).padStart(2, '0'); };
        return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
    }
    function toUtc(value) { return value ? new Date(value).toISOString() : null; }
    function openPicker(input) { try { input.showPicker(); } catch (e) { /* trình duyệt cũ dùng biểu tượng lịch */ } }

    function fill(b) {
        var v = b.values;
        el('GameId').innerHTML = b.games.map(function (g) {
            return '<option value="' + g.gameId + '">' + Ui.escapeHtml(g.title) + '</option>';
        }).join('');
        TEXT.forEach(function (f) { el(f).value = v[camel(f)] || ''; });
        NUMBER.concat(OPTIONAL_NUMBER).forEach(function (f) { el(f).value = v[camel(f)] === null || v[camel(f)] === undefined ? '' : v[camel(f)]; });
        el('EntryFee').value = v.entryFee;
        CHECK.forEach(function (f) { el(f).checked = !!v[camel(f)]; });
        DATES.forEach(function (f) { el(f).value = toLocalInput(v[camel(f)]); });
        el('approved-hint').textContent = b.approvedCount ? 'Đã duyệt ' + b.approvedCount + ' đội; số đội tối đa không được nhỏ hơn số này.' : '';
    }

    function applyLocks(b) {
        ['INFO', 'SETUP', 'TIMELINE', 'FORMAT'].forEach(function (group) {
            var reason = b.lockReasons[group];
            var banner = document.querySelector('[data-lock="' + group + '"]');
            banner.hidden = !reason;
            banner.textContent = reason || '';
            document.querySelector('fieldset[data-group="' + group + '"]').disabled = !!reason;
        });
        // Mốc đã qua của giải đã xuất bản giữ nguyên.
        DATES.forEach(function (f) {
            var past = b.pastMilestones.indexOf(f) >= 0;
            document.querySelector('[data-past="' + f + '"]').hidden = !past;
            if (!b.lockReasons.TIMELINE) el(f).disabled = past;
        });
        var anyEditable = ['INFO', 'SETUP', 'TIMELINE', 'FORMAT'].some(function (g) { return !b.lockReasons[g]; });
        el('save-btn').hidden = !anyEditable;
    }

    function syncVisibility() {
        var format = el('Format').value;
        el('venue-field').hidden = el('IsOnline').checked;
        el('groups-field').hidden = !(format === 'ROUND_ROBIN' || format === 'HYBRID');
        el('advance-field').hidden = format !== 'HYBRID';
        el('third-field').hidden = !(format === 'SINGLE_ELIMINATION' || format === 'HYBRID');
        if (el('third-field').hidden) el('HasThirdPlaceMatch').checked = false;
        var url = el('BannerUrl').value.trim();
        var img = el('banner-preview');
        img.hidden = !url;
        if (url && img.getAttribute('src') !== url) img.src = url;
    }

    function render(b) {
        board = b;
        el('page-title').textContent = 'Sửa thông tin · ' + b.values.name;
        el('page-sub').innerHTML = 'Trạng thái: <span class="badge ' + Ui.escapeHtml(b.statusClass) + '">' + Ui.escapeHtml(b.statusLabel) + '</span>' +
            (b.isBracketLocked ? ' · Nhánh đấu đã khóa' : '') + '. Thể lệ sửa ở <a href="/tournament/rulebook.html?id=' + tournamentId + '">trang Thể lệ giải</a>.';
        el('edit-root').innerHTML = '';
        el('edit-form').hidden = false;
        fill(b);
        applyLocks(b);
        syncVisibility();
    }

    function body() {
        var data = {};
        TEXT.forEach(function (f) { data[camel(f)] = el(f).value; });
        NUMBER.forEach(function (f) { data[camel(f)] = parseInt(el(f).value || '0', 10); });
        OPTIONAL_NUMBER.forEach(function (f) { data[camel(f)] = el(f).value === '' ? null : parseInt(el(f).value, 10); });
        data.entryFee = parseFloat(el('EntryFee').value || '0');
        CHECK.forEach(function (f) { data[camel(f)] = el(f).checked; });
        // Ô bị khóa vẫn gửi giá trị cũ để server biết không có gì đổi.
        DATES.forEach(function (f) { data[camel(f)] = el(f).value ? toUtc(el(f).value) : (f === 'EndAt' ? null : board.values[camel(f)]); });
        if (el('GroupCount').closest('.field').hidden) data.groupCount = null;
        if (el('AdvancePerGroup').closest('.field').hidden) data.advancePerGroup = null;
        return data;
    }

    function wire() {
        var form = el('edit-form');
        ['Format', 'IsOnline'].forEach(function (id) { el(id).addEventListener('change', syncVisibility); });
        el('BannerUrl').addEventListener('change', syncVisibility);
        DATES.forEach(function (f) {
            var input = el(f);
            input.addEventListener('click', function () { openPicker(input); });
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Tab') return;
                if (e.key === 'Delete' || e.key === 'Backspace') { if (f === 'EndAt') input.value = ''; e.preventDefault(); return; }
                e.preventDefault();
                if (e.key === 'Enter' || e.key === ' ') openPicker(input);
            });
        });
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash());
            Ui.clearFieldErrors(form);
            var btn = el('save-btn');
            btn.disabled = true;
            try {
                var res = await Api.put('/api/tournaments/' + tournamentId + '/settings', body());
                render(res.data);
                Ui.showAlert(flash(), 'success', res.message);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                var keys = Object.keys(err.fieldErrors || {});
                Ui.showAlert(flash(), 'danger', keys.length === 1 ? err.fieldErrors[keys[0]] : keys.length ? 'Vui lòng kiểm tra lại thông tin.' : err.message);
                var first = keys.length && form.querySelector('[data-field-error="' + keys[0] + '"]');
                if (first) first.closest('.edit-section').scrollIntoView({ block: 'start' });
            } finally {
                btn.disabled = false;
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth();
        if (!me) return;

        tournamentId = Ui.qsInt('id');
        if (tournamentId === null) {
            showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.');
            return;
        }
        el('back-link').href = '/tournament/manage.html?id=' + tournamentId;
        try {
            var res = await Api.get('/api/tournaments/' + tournamentId + '/settings');
            render(res.data);
            wire();
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại.');
            else if (err.status === 403) showEmpty('Bạn không thể sửa giải này', 'Chỉ Ban tổ chức của giải được sửa thông tin giải.');
            else showEmpty('Không tải được thông tin giải', err.message);
        }
    });
})();
