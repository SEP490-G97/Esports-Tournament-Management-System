// Khối "Thể lệ & bộ luật" dùng chung cho bước "Thể lệ" của trình tạo giải và trang Sửa thông tin giải.
//   - Thể lệ: soạn trên web (định dạng cơ bản) và/hoặc tải file PDF (≤ 10 MB) — máy chủ lọc HTML, kiểm chữ ký PDF.
//   - Bộ luật theo tựa game: chọn một bộ luật mẫu hợp với số người của giải (không bắt buộc).
// RulesEditor.mount(root, tournamentId) -> Promise<{ save(): Promise<{ changed, messages }> }>
window.RulesEditor = (function () {
    var MAX_PDF = 10 * 1024 * 1024;
    function esc(s) { return Ui.escapeHtml(s == null ? '' : String(s)); }
    function data(res) { return res && res.data !== undefined ? res.data : res; }
    var FORMAT = { SINGLE_ELIMINATION: 'Loại trực tiếp', DOUBLE_ELIMINATION: 'Loại kép', ROUND_ROBIN: 'Vòng tròn', HYBRID: 'Vòng bảng rồi loại trực tiếp' };

    function toolbar(uid) {
        function b(cmd, value, label, title) {
            return '<button type="button" class="rte-btn" data-cmd="' + cmd + '"' + (value ? ' data-value="' + value + '"' : '') + ' title="' + title + '">' + label + '</button>';
        }
        return '<div class="rte-toolbar" role="toolbar" aria-label="Định dạng thể lệ" aria-controls="' + uid + '">' +
            b('formatBlock', 'H3', 'Tiêu đề', 'Tiêu đề mục') + b('formatBlock', 'P', 'Đoạn', 'Đoạn văn thường') + '<span class="rte-sep"></span>' +
            b('bold', '', '<b>B</b>', 'In đậm') + b('italic', '', '<i>I</i>', 'In nghiêng') + '<span class="rte-sep"></span>' +
            b('insertUnorderedList', '', '• Danh sách', 'Danh sách chấm') + b('insertOrderedList', '', '1. Danh sách', 'Danh sách số') + '</div>';
    }

    async function mount(root, tournamentId) {
        var uid = 'rte-' + tournamentId + '-' + Math.random().toString(36).slice(2, 7);
        var rulebook = null, board = null;
        try { rulebook = data(await Api.get('/api/tournaments/' + tournamentId + '/rulebook')); } catch (e) { rulebook = null; }
        try { board = data(await Api.get('/api/tournaments/' + tournamentId + '/ruleset')); } catch (e) { board = null; }

        var canEditRulebook = !rulebook || rulebook.canEdit !== false;
        var options = (board && board.options || []).filter(function (o) { return o.isActive; });
        root.innerHTML =
            '<div class="rules-block">' +
            '<div class="rules-head"><h4>Thể lệ giải</h4><span class="hint">Soạn trực tiếp, tải file PDF, hoặc cả hai.</span></div>' +
            (canEditRulebook ? '' : '<div class="pending-banner">Giải đã kết thúc hoặc đã hủy — không sửa thể lệ được.</div>') +
            toolbar(uid) +
            '<div id="' + uid + '" class="rte-editor rules-editor" contenteditable="' + canEditRulebook + '" role="textbox" aria-multiline="true" aria-label="Nội dung thể lệ"' +
            ' data-placeholder="Ví dụ: Điều kiện tham gia, thể thức, lịch thi đấu, cách tính điểm, quy định check-in, xử lý tranh chấp…"></div>' +
            '<div class="rules-pdf">' +
            '<label class="btn btn-ghost btn-sm mb-0" for="' + uid + '-pdf">Chọn file PDF</label>' +
            '<input id="' + uid + '-pdf" type="file" accept="application/pdf,.pdf" hidden ' + (canEditRulebook ? '' : 'disabled') + ' />' +
            '<span class="hint" data-pdf-status>' + (rulebook && rulebook.pdfUrl
                ? 'Đang dùng: <a href="' + esc(Ui.safeUrl(rulebook.pdfUrl)) + '" target="_blank" rel="noopener">xem file PDF hiện tại</a> — chọn file mới để thay.'
                : 'Chưa có file PDF (tối đa 10 MB).') + '</span>' +
            '<span class="field-error" data-err="pdf"></span></div>' +
            (rulebook && rulebook.version > 1 ? '<div class="hint mt-1">Giải đã công bố: mỗi lần lưu thể lệ tạo phiên bản mới và báo cho các đội đã đăng ký.</div>' : '') +
            '</div>' +
            '<div class="rules-block">' +
            '<div class="rules-head"><h4>Bộ luật theo tựa game</h4><span class="hint">Không bắt buộc — chọn bộ luật mẫu của tựa game.</span></div>' +
            (!board ? '<div class="hint">Không tải được danh sách bộ luật.</div>'
                : board.blockedReason ? '<div class="pending-banner">' + esc(board.blockedReason) + '</div>'
                : options.length === 0 ? '<div class="hint">Tựa game ' + esc(board.gameTitle) + ' chưa có bộ luật mẫu.</div>'
                : '<div class="rules-options" role="radiogroup" aria-label="Bộ luật">' +
                  // Máy chủ không cho bỏ chọn bộ luật đã chọn -> lựa chọn "Không dùng" chỉ có khi chưa chọn.
                  (board.selectedRulesetId ? '' : '<label class="rules-option"><input type="radio" name="' + uid + '-rs" value="" checked' + (board.canEdit ? '' : ' disabled') + ' /><span><span class="ro-name">Không dùng bộ luật mẫu</span></span></label>') +
                  options.map(function (o) {
                      var starters = o.minStarters === o.maxStarters ? String(o.minStarters) : o.minStarters + '–' + o.maxStarters;
                      var fits = o.fitsTournament || o.gameRulesetId === board.selectedRulesetId;
                      return '<label class="rules-option' + (fits ? '' : ' disabled') + '"><input type="radio" name="' + uid + '-rs" value="' + Number(o.gameRulesetId) + '"' +
                          (o.gameRulesetId === board.selectedRulesetId ? ' checked' : '') + (board.canEdit && fits ? '' : ' disabled') + ' />' +
                          '<span><span class="ro-name">' + esc(o.name) + '</span><span class="ro-sub">' + esc(starters + ' chính thức, tối đa ' + o.maxSubstitutes + ' dự bị · ' + (FORMAT[o.defaultFormat] || o.defaultFormat)) + '</span>' +
                          (fits ? '' : '<span class="ro-sub text-warn">' + esc(o.mismatchReason || 'Không hợp với số người của giải') + '</span>') + '</span></label>';
                  }).join('') + '</div>') +
            '</div>';

        var editor = document.getElementById(uid);
        var original = (rulebook && rulebook.content) || '';
        editor.innerHTML = original;
        root.querySelectorAll('.rte-toolbar').forEach(function (bar) {
            bar.addEventListener('mousedown', function (e) { if (e.target.closest('.rte-btn')) e.preventDefault(); });
        });
        root.querySelectorAll('.rte-btn[data-cmd]').forEach(function (btn) {
            btn.addEventListener('click', function () { editor.focus(); document.execCommand(btn.dataset.cmd, false, btn.dataset.value || null); });
        });
        // Dán chỉ lấy chữ thuần để không kéo theo định dạng lạ từ Word / web.
        editor.addEventListener('paste', function (e) { e.preventDefault(); document.execCommand('insertText', false, e.clipboardData.getData('text/plain')); });

        var pdfInput = document.getElementById(uid + '-pdf');
        var pdfStatus = root.querySelector('[data-pdf-status]');
        var pdfErr = root.querySelector('[data-err="pdf"]');
        pdfInput.addEventListener('change', function () {
            pdfErr.textContent = '';
            var f = pdfInput.files[0];
            if (!f) return;
            if (!/\.pdf$/i.test(f.name)) { pdfErr.textContent = 'Chỉ nhận file .pdf.'; pdfInput.value = ''; return; }
            if (f.size === 0 || f.size > MAX_PDF) { pdfErr.textContent = 'File PDF phải lớn hơn 0 và tối đa 10 MB.'; pdfInput.value = ''; return; }
            pdfStatus.textContent = 'Sẽ tải lên khi lưu: ' + f.name + ' (' + (f.size / 1048576).toFixed(1) + ' MB)';
        });
        var initialRs = board && board.selectedRulesetId ? String(board.selectedRulesetId) : '';

        function hasText(html) { var d = document.createElement('div'); d.innerHTML = html; return (d.textContent || '').trim().length > 0; }

        async function save() {
            var messages = [], changed = false;
            pdfErr.textContent = '';
            var html = editor.innerHTML.trim();
            if (canEditRulebook && html !== original && hasText(html)) {
                if (html.length > 100000) throw new Error('Thể lệ quá dài (tối đa 100.000 ký tự).');
                var res = await Api.put('/api/tournaments/' + tournamentId + '/rulebook', { content: html, baseVersion: (rulebook && rulebook.currentVersion) || 1 });
                var saved = data(res);
                if (saved && saved.version && rulebook) rulebook.currentVersion = saved.version;
                original = html;
                changed = true;
                messages.push(res.message || 'Đã lưu thể lệ.');
            }
            var f = pdfInput.files[0];
            if (canEditRulebook && f) {
                var fd = new FormData();
                fd.append('file', f);
                try {
                    var up = await Api.postForm('/api/tournaments/' + tournamentId + '/rulebook/pdf', fd);
                    var url = data(up);
                    pdfInput.value = '';
                    pdfStatus.innerHTML = 'Đang dùng: <a href="' + esc(Ui.safeUrl(url)) + '" target="_blank" rel="noopener">xem file PDF</a> — chọn file mới để thay.';
                    changed = true;
                    messages.push(up.message || 'Đã tải file PDF thể lệ.');
                } catch (err) {
                    pdfErr.textContent = err.message;
                    throw err;
                }
            }
            var picked = root.querySelector('input[name="' + uid + '-rs"]:checked');
            if (board && board.canEdit && picked && picked.value && picked.value !== initialRs) {
                var r = await Api.put('/api/tournaments/' + tournamentId + '/ruleset', { gameRulesetId: parseInt(picked.value, 10) });
                initialRs = picked.value;
                changed = true;
                messages.push(r.message || 'Đã chọn bộ luật.');
            }
            return { changed: changed, messages: messages };
        }

        return { save: save, hasContent: function () { return hasText(editor.innerHTML) || !!(rulebook && rulebook.pdfUrl) || !!pdfInput.files[0]; } };
    }

    return { mount: mount };
})();
