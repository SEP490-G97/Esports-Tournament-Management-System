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
        return date.toLocaleString('vi-VN');
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
        var newest = versions[0].version;
        var older = versions.length > 1 ? versions[1].version : newest;

        document.getElementById('history-root').innerHTML =
            '<div class="rulebook-compare">' +
            '<div class="card-etoms"><div class="body"><div class="field">' + selectHtml('version-left', versions, older) + '</div>' +
            '<div id="content-left" class="rulebook-content"></div></div></div>' +
            '<div class="card-etoms"><div class="body"><div class="field">' + selectHtml('version-right', versions, newest) + '</div>' +
            '<div id="content-right" class="rulebook-content"></div></div></div>' +
            '</div>';

        var left = document.getElementById('version-left');
        var right = document.getElementById('version-right');
        left.addEventListener('change', function () { loadInto('content-left', left.value); });
        right.addEventListener('change', function () { loadInto('content-right', right.value); });
        loadInto('content-left', older);
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
            if (!versions.length) {
                showEmpty('Giải chưa có thể lệ', 'Ban tổ chức chưa đăng thể lệ cho giải này.');
                return;
            }
            render(versions);
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại hoặc chưa được công khai.');
            else showEmpty('Không tải được thể lệ', err.message);
        }
    });
})();
