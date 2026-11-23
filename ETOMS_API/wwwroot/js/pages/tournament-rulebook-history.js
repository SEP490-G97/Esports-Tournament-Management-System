(function () {
    var tournamentId = null;

    function showEmpty(title, message) {
        document.getElementById('history-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    // Server trả giờ UTC không kèm múi giờ.
    function formatTime(value) {
        if (!value) return '';
        var date = new Date(/Z$/.test(value) ? value : value + 'Z');
        return Ui.fmtDateTime(date);
    }

    function versionLabel(v) {
        var label = 'Phiên bản ' + v.version + (v.isCurrent ? ' (hiện hành)' : '');
        return v.savedAt ? label + ' · ' + formatTime(v.savedAt) : label;
    }

    function selectHtml(id, versions, selected) {
        var options = versions.map(function (v) {
            return '<option value="' + v.version + '"' + (v.version === selected ? ' selected' : '') + '>' + Ui.escapeHtml(versionLabel(v)) + '</option>';
        }).join('');
        return '<select id="' + id + '" class="select">' + options + '</select>';
    }

    // Content đã được server lọc nên gán thẳng vào innerHTML.
    async function loadInto(targetId, version) {
        var target = document.getElementById(targetId);
        try {
            var rulebook = await Api.get('/api/tournaments/' + tournamentId + '/rulebook?version=' + version);
            target.innerHTML = rulebook.content || '<p>Phiên bản này chưa có nội dung.</p>';
        } catch (err) {
            target.textContent = err.message;
        }
    }

    function render(versions) {
        // Người xem đọc MỘT bản (bản hiện hành); chỉ khi có nhiều phiên bản mới hiện ô chọn để xem bản cũ.
        var newest = versions[0].version;
        document.getElementById('history-root').innerHTML =
            '<div class="card-etoms"><div class="body">' +
            (versions.length > 1 ? '<div class="field rulebook-version-pick">' + selectHtml('version-right', versions, newest) + '</div>' : '') +
            '<div id="content-right" class="rulebook-content"></div></div></div>';
        var right = document.getElementById('version-right');
        if (right) right.addEventListener('change', function () { loadInto('content-right', right.value); });
        loadInto('content-right', newest);
    }

    document.addEventListener('DOMContentLoaded', async function () {
        tournamentId = Ui.qsInt('id');
        if (tournamentId === null) {
            showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.');
            return;
        }

        try {
            var rulebook = await Api.get('/api/tournaments/' + tournamentId + '/rulebook');
            document.getElementById('page-title').textContent = 'Thể lệ giải ' + rulebook.tournamentName;

            var versions = await Api.get('/api/tournaments/' + tournamentId + '/rulebook/versions');
            var pdf = Ui.safeUrl(rulebook.pdfUrl);
            // File PDF thể lệ (nếu Ban tổ chức tải lên) hiện ngay đầu trang.
            var pdfBar = pdf ? '<div class="card-etoms mb-3"><div class="body d-flex flex-wrap align-items-center justify-content-between gap-2">' +
                '<span>Thể lệ đầy đủ dạng file PDF</span><a class="btn btn-primary btn-sm" href="' + Ui.escapeHtml(pdf) + '" target="_blank" rel="noopener">Mở file PDF</a></div></div>' : '';
            if (!versions.length || (!rulebook.content && versions.length <= 1)) {
                if (pdf) { document.getElementById('history-root').innerHTML = pdfBar; return; }
                showEmpty('Giải chưa có thể lệ', 'Ban tổ chức chưa đăng thể lệ cho giải này.');
                return;
            }
            render(versions);
            if (pdfBar) document.getElementById('history-root').insertAdjacentHTML('afterbegin', pdfBar);
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại hoặc chưa được công khai.');
            else showEmpty('Không tải được thể lệ', err.message);
        }
    });
})();
