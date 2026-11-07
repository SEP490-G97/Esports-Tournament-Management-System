(function () {
    var STARTER_TEMPLATE =
        '<h2>Thể lệ thi đấu</h2><p><br></p>' +
        '<h2>Quy định kỷ luật</h2><p><br></p>' +
        '<h2>Hướng dẫn nộp minh chứng</h2><p><br></p>';

    var tournamentId = null;
    var baseVersion = 1;
    var hasSavedContent = false;
    var isDraft = false;

    function showEmpty(title, message, linkHtml) {
        document.getElementById('rulebook-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p>' + (linkHtml || '') + '</div></div>';
    }

    function updateVersionLine(name) {
        document.getElementById('page-title').textContent = 'Thể lệ giải ' + name;
        var sub;
        if (isDraft) sub = 'Giải còn nháp nên thể lệ giữ phiên bản ' + baseVersion + ' đến khi xuất bản.';
        else if (hasSavedContent) sub = 'Đang sửa từ phiên bản ' + baseVersion + '. Lưu sẽ tạo phiên bản mới, các đội đã đăng ký vẫn giữ bản họ đã chấp nhận.';
        else sub = 'Giải chưa có thể lệ. Lần lưu đầu tiên sẽ là phiên bản ' + baseVersion + '.';
        document.getElementById('page-sub').textContent = sub;
    }

    function wireToolbar(editor) {
        // Giữ vùng chọn trong editor khi bấm nút trên thanh công cụ.
        document.querySelector('.rte-toolbar').addEventListener('mousedown', function (e) {
            if (e.target.closest('.rte-btn')) e.preventDefault();
        });

        document.querySelectorAll('.rte-btn[data-cmd]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                editor.focus();
                document.execCommand(btn.dataset.cmd, false, btn.dataset.value || null);
            });
        });

        document.getElementById('rte-link').addEventListener('click', function () {
            var url = window.prompt('Nhập địa chỉ liên kết (bắt đầu bằng http:// hoặc https://):', 'https://');
            if (!url) return;
            if (!/^https?:\/\//i.test(url)) {
                Ui.showAlert(document.getElementById('flash'), 'danger', 'Liên kết phải bắt đầu bằng http:// hoặc https://.');
                return;
            }
            editor.focus();
            document.execCommand('createLink', false, url);
        });

        // Dán dạng chữ thuần để không kéo theo định dạng từ Word hay trang web khác.
        editor.addEventListener('paste', function (e) {
            e.preventDefault();
            document.execCommand('insertText', false, e.clipboardData.getData('text/plain'));
        });
    }

    function wireSubmit(editor, name) {
        var form = document.getElementById('rulebook-form');
        var flash = document.getElementById('flash');
        var saveBtn = document.getElementById('save-btn');

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);

            if (hasSavedContent && !isDraft) {
                var ok = await window.etomsConfirm('Lưu sẽ tạo phiên bản thể lệ mới và thông báo cho các đội đã đăng ký. Bản các đội đã chấp nhận không thay đổi. Tiếp tục?');
                if (!ok) return;
            }

            saveBtn.disabled = true;
            try {
                var res = await Api.put('/api/tournaments/' + tournamentId + '/rulebook', {
                    content: editor.innerHTML,
                    baseVersion: baseVersion
                });
                baseVersion = res.data.version;
                if (res.data.changed) hasSavedContent = true;
                updateVersionLine(name);
                var detail = res.data.notifiedCaptains > 0 ? ' Đã thông báo cho ' + res.data.notifiedCaptains + ' đội trưởng.' : '';
                Ui.showAlert(flash, 'success', res.message + detail);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.message);
            } finally {
                saveBtn.disabled = false;
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

        var rulebook;
        try {
            rulebook = await Api.get('/api/tournaments/' + tournamentId + '/rulebook');
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại hoặc chưa được công khai.');
            else showEmpty('Không tải được thể lệ', err.message);
            return;
        }

        var historyUrl = '/tournament/rulebook-history.html?id=' + tournamentId;
        if (!rulebook.canEdit) {
            showEmpty('Bạn không thể sửa thể lệ này',
                'Chỉ Ban tổ chức của giải được sửa, và giải phải chưa kết thúc hoặc chưa bị hủy.',
                '<a class="btn btn-ghost" href="' + historyUrl + '">Xem thể lệ</a>');
            return;
        }

        baseVersion = rulebook.currentVersion;
        hasSavedContent = !!rulebook.content;
        isDraft = rulebook.tournamentStatus === 'DRAFT';

        var editor = document.getElementById('editor');
        editor.innerHTML = rulebook.content || STARTER_TEMPLATE;
        document.getElementById('history-link').href = historyUrl;
        updateVersionLine(rulebook.tournamentName);

        document.getElementById('rulebook-root').innerHTML = '';
        document.getElementById('editor-card').hidden = false;
        wireToolbar(editor);
        wireSubmit(editor, rulebook.tournamentName);
    });
})();
