(function () {
    var flash = function () { return document.getElementById('flash'); };
    var me = null;

    function showError(message) {
        Ui.showAlert(flash(), 'danger', message);
        flash().scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    }

    // Không có lựa chọn hợp lệ thì khóa form và ghi rõ lý do, KHÔNG điền giá trị giả.
    function lockForm(selectId, message, linkHtml) {
        var select = document.getElementById(selectId);
        select.innerHTML = '<option value="">' + Ui.escapeHtml(message) + '</option>';
        select.disabled = true;
        var form = select.closest('form');
        form.querySelectorAll('input, textarea, button[type="submit"]').forEach(function (el) { el.disabled = true; });
        if (linkHtml) select.insertAdjacentHTML('afterend', '<div class="hint mt-1">' + linkHtml + '</div>');
    }

    async function loadSelectOptions() {
        // LFP: chỉ đội mình làm ĐỘI TRƯỞNG mới được đăng tin tuyển (FA-01).
        try {
            var teams = (await Api.get('/api/teams/my-teams')).filter(function (t) { return t.isCaptain; });
            if (teams.length > 0) {
                document.getElementById('lfpTeamId').innerHTML = '<option value="">-- Chọn đội --</option>' + teams.map(function (t) {
                    return '<option value="' + t.teamId + '">' + Ui.escapeHtml(t.name) + ' [' + Ui.escapeHtml(t.tag) + '] · ' + Ui.escapeHtml(t.gameTitle) + '</option>';
                }).join('');
            } else {
                lockForm('lfpTeamId', 'Bạn chưa là đội trưởng đội nào',
                    'Chỉ đội trưởng được đăng tin tuyển. <a href="/team/create.html">Tạo đội mới</a> · <a href="/team/mine.html">Các đội của tôi</a>');
            }
        } catch (e) {
            lockForm('lfpTeamId', 'Không tải được danh sách đội', Ui.escapeHtml(e.message || ''));
        }

        try {
            var games = await Api.get('/api/teams/games');
            if (Array.isArray(games) && games.length > 0) {
                document.getElementById('lftGameId').innerHTML = '<option value="">-- Chọn tựa game --</option>' + games.map(function (g) {
                    return '<option value="' + g.gameId + '">' + Ui.escapeHtml(g.title) + '</option>';
                }).join('');
            } else {
                lockForm('lftGameId', 'Chưa có tựa game nào đang mở');
            }
        } catch (e) {
            lockForm('lftGameId', 'Không tải được danh sách game', Ui.escapeHtml(e.message || ''));
        }
    }

    function formatDate(value) {
        if (!value) return '';
        var d = new Date(/Z$/.test(value) ? value : value + 'Z');
        return isNaN(d.getTime()) ? '' : d.toLocaleDateString('vi-VN');
    }

    // Bảng tin theo loại (LFP / LFT), mỗi loại một khung riêng.
    async function loadBoard(type) {
        var container = document.getElementById(type === 'LFP' ? 'board-lfp' : 'board-lft');
        container.innerHTML = '<div class="empty"><h3>Đang tải bảng tin...</h3></div>';
        try {
            var res = await Api.get('/api/Recruitment/board?PostType=' + type + '&PageIndex=1&PageSize=20');
            var items = res.data || [];
            if (!items.length) {
                container.innerHTML = '<div class="empty"><h3>Chưa có tin đăng nào</h3><p>' +
                    (type === 'LFP' ? 'Chưa đội nào đang tuyển thành viên.' : 'Chưa tuyển thủ nào đăng tin tìm đội.') + '</p></div>';
                return;
            }
            var canApply = me && me.role === 'PLAYER';
            container.innerHTML = items.map(function (item) {
                var target = item.teamName
                    ? 'Đội: <b>' + Ui.escapeHtml(item.teamName) + (item.teamTag ? ' [' + Ui.escapeHtml(item.teamTag) + ']' : '') + '</b> · ' + Ui.escapeHtml(item.gameTitle || '')
                    : 'Tựa game: <b>' + Ui.escapeHtml(item.gameTitle || 'Chung') + '</b>';
                return '<article class="fa-post"><div style="flex:1;min-width:240px">' +
                    '<span class="badge ' + (type === 'LFP' ? 'badge-active' : 'badge-gold') + '">' + type + '</span> ' +
                    '<span class="hint">' + target + '</span>' +
                    '<h4>' + Ui.escapeHtml(item.title) + '</h4>' +
                    '<p>' + Ui.escapeHtml(item.content) + '</p>' +
                    '<small>Người đăng: ' + Ui.escapeHtml(item.authorName) + (formatDate(item.createdAt) ? ' · ' + formatDate(item.createdAt) : '') + '</small>' +
                    '</div>' +
                    (type === 'LFP' && canApply && Number(item.authorAccountId) !== Number(me.userId) ? '<button type="button" class="btn btn-primary" data-apply="' + Number(item.postId) + '">Ứng tuyển</button>' : '') +
                    '</article>';
            }).join('');
            container.querySelectorAll('[data-apply]').forEach(function (btn) {
                btn.addEventListener('click', function () { applyPost(Number(btn.dataset.apply), btn); });
            });
        } catch (err) {
            container.innerHTML = '<div class="empty"><h3>Không tải được bảng tin</h3><p>' + Ui.escapeHtml(err.message) + '</p></div>';
        }
    }

    // FA-04: ứng tuyển theo tin LFP — lời nhắn không bắt buộc, tối đa 500 ký tự (khớp máy chủ).
    async function applyPost(postId, button) {
        var message = await window.etomsPrompt('Lời nhắn gửi đội trưởng (không bắt buộc):', {
            title: 'Ứng tuyển vào đội', required: false, maxLength: 500, okText: 'Gửi đơn',
            placeholder: 'Giới thiệu ngắn về bạn, vị trí, thời gian có thể luyện tập...'
        });
        if (message === null) return;
        button.disabled = true;
        try {
            await Api.post('/api/Recruitment/apply', { postId: postId, message: message });
            Ui.showAlert(flash(), 'success', 'Đã nộp đơn ứng tuyển. Đơn đang chờ đội trưởng duyệt.');
        } catch (err) {
            showError(err.message || 'Không nộp được đơn ứng tuyển.');
            button.disabled = false;
        }
    }

    // Kiểm tra phía trình duyệt — cùng luật với DataAnnotations của CreateLFPPostDto / CreateLFTPostDto.
    function validatePost(form, fields) {
        var errors = {};
        fields.forEach(function (f) {
            var el = document.getElementById(f.id);
            var value = (el.value || '').trim();
            if (f.select) { if (!value) errors[f.key] = f.required; return; }
            if (f.required && !value) errors[f.key] = f.required;
            else if (f.min && value && value.length < f.min) errors[f.key] = f.label + ' phải từ ' + f.min + ' đến ' + f.max + ' ký tự.';
            else if (value.length > f.max) errors[f.key] = f.label + ' tối đa ' + f.max + ' ký tự.';
        });
        Ui.clearFieldErrors(form);
        Ui.applyFieldErrors(form, errors);
        return Object.keys(errors).length === 0;
    }

    function wirePostForm(formId, url, fields, buildPayload, successText, tab) {
        var form = document.getElementById(formId);
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash());
            if (!validatePost(form, fields)) return;
            var submit = form.querySelector('button[type="submit"]');
            submit.disabled = true;
            try {
                await Api.post(url, buildPayload());
                form.reset();
                Ui.showAlert(flash(), 'success', successText);
                await loadBoard(tab);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                showError(err.message || 'Không đăng được tin.');
            } finally {
                submit.disabled = false;
            }
        });
    }

    function switchTab(type) {
        document.querySelectorAll('.fa-tabs [data-tab]').forEach(function (b) {
            b.setAttribute('aria-selected', b.dataset.tab === type ? 'true' : 'false');
            b.setAttribute('aria-pressed', b.dataset.tab === type ? 'true' : 'false');
        });
        document.getElementById('panel-lfp').hidden = type !== 'LFP';
        document.getElementById('panel-lft').hidden = type !== 'LFT';
        try { history.replaceState(null, '', '#' + type.toLowerCase()); } catch (e) { }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        me = await Ui.requireAuth();
        if (!me) return;
        Ui.consumeFlash(flash());

        document.querySelectorAll('.fa-tabs [data-tab]').forEach(function (b) {
            b.addEventListener('click', function () { switchTab(b.dataset.tab); });
        });
        switchTab(location.hash === '#lft' ? 'LFT' : 'LFP');

        wirePostForm('lfpForm', '/api/Recruitment/lfp', [
            { id: 'lfpTeamId', key: 'TeamId', select: true, required: 'Vui lòng chọn đội tuyển.' },
            { id: 'lfpRequiredRole', key: 'RequiredRole', label: 'Vị trí cần tuyển', required: 'Vui lòng nhập vị trí cần tuyển.', min: 2, max: 50 },
            { id: 'lfpRequiredRank', key: 'RequiredRank', label: 'Rank yêu cầu', required: 'Vui lòng nhập rank yêu cầu.', max: 50 },
            { id: 'lfpDescription', key: 'Description', label: 'Mô tả', max: 1000 }
        ], function () {
            return {
                teamId: parseInt(document.getElementById('lfpTeamId').value, 10),
                requiredRole: document.getElementById('lfpRequiredRole').value.trim(),
                requiredRank: document.getElementById('lfpRequiredRank').value.trim(),
                description: document.getElementById('lfpDescription').value.trim()
            };
        }, 'Đã đăng tin tuyển thành viên.', 'LFP');

        wirePostForm('lftForm', '/api/Recruitment/lft', [
            { id: 'lftGameId', key: 'GameId', select: true, required: 'Vui lòng chọn tựa game.' },
            { id: 'lftPreferredRole', key: 'PreferredRole', label: 'Vị trí sở trường', required: 'Vui lòng nhập vị trí sở trường.', min: 2, max: 50 },
            { id: 'lftCurrentRank', key: 'CurrentRank', label: 'Rank hiện tại', required: 'Vui lòng nhập rank hiện tại.', max: 50 },
            { id: 'lftDescription', key: 'Description', label: 'Mô tả', max: 1000 }
        ], function () {
            return {
                gameId: parseInt(document.getElementById('lftGameId').value, 10),
                preferredRole: document.getElementById('lftPreferredRole').value.trim(),
                currentRank: document.getElementById('lftCurrentRank').value.trim(),
                description: document.getElementById('lftDescription').value.trim()
            };
        }, 'Đã đăng tin tìm đội.', 'LFT');

        if (me.role === 'PLAYER') await loadSelectOptions();
        await Promise.all([loadBoard('LFP'), loadBoard('LFT')]);
    });
})();
