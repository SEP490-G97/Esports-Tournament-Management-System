// UC-46 — Ban tổ chức thêm, sửa, xóa link livestream của giải và gán vào trận.
// Kiểm tra ở trình duyệt trùng thông báo với máy chủ; máy chủ vẫn kiểm lại toàn bộ.
(function () {
    var esc = function (s) { return Ui.escapeHtml(s == null ? '' : String(s)); };
    var tournamentId, data = null, editingId = null;
    var flash;

    function showEmpty(title, text) {
        document.getElementById('stream-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>' + esc(title) + '</h3><p>' + esc(text || '') + '</p></div></div>';
    }

    // Nhận diện nền tảng giống máy chủ để báo trước cho Ban tổ chức (máy chủ mới là nơi quyết định).
    // Nhận được link của mọi nền tảng (góp ý 25/11); đây chỉ là gợi ý tên nền tảng.
    var KNOWN = [['youtu.be', 'YouTube'], ['youtube.com', 'YouTube'], ['fb.watch', 'Facebook'], ['facebook.com', 'Facebook'], ['twitch.tv', 'Twitch'],
        ['tiktok.com', 'TikTok'], ['kick.com', 'Kick'], ['vimeo.com', 'Vimeo'], ['dailymotion.com', 'Dailymotion'], ['dai.ly', 'Dailymotion'],
        ['bilibili.com', 'Bilibili'], ['nimo.tv', 'Nimo TV'], ['trovo.live', 'Trovo'], ['huya.com', 'Huya'], ['douyin.com', 'Douyin'],
        ['instagram.com', 'Instagram'], ['x.com', 'X'], ['twitter.com', 'X'], ['rumble.com', 'Rumble']];
    function platformOf(url) {
        try {
            var host = new URL(url).hostname.toLowerCase();
            var is = function (d) { return host === d || host.endsWith('.' + d); };
            for (var i = 0; i < KNOWN.length; i++) if (is(KNOWN[i][0])) return KNOWN[i][1];
            return host.replace(/^www\./, '');
        } catch (e) { return ''; }
    }

    function validate(form) {
        var errors = {};
        var url = form.StreamUrl.value.trim();
        if (!url) errors.StreamUrl = 'Vui lòng nhập link livestream.';
        else if (url.length > 500) errors.StreamUrl = 'Link livestream tối đa 500 ký tự.';
        else if (!/^https:\/\//i.test(url)) errors.StreamUrl = 'Link livestream phải bắt đầu bằng https://';
        else { try { new URL(url); } catch (e) { errors.StreamUrl = 'Link livestream không hợp lệ.'; } }
        if (form.Title.value.trim().length > 200) errors.Title = 'Tiêu đề tối đa 200 ký tự.';
        return errors;
    }

    function formHtml() {
        var editing = editingId ? data.streams.find(function (s) { return s.livestreamId === editingId; }) : null;
        var matchOptions = '<option value="">Không gán trận (livestream chung của giải)</option>' + data.matches.map(function (m) {
            return '<option value="' + m.matchId + '"' + (editing && editing.matchId === m.matchId ? ' selected' : '') + '>' + esc(m.label) + ' — ' + esc(m.status) + '</option>';
        }).join('');
        var status = editing ? editing.status : 'SCHEDULED';
        return '<section class="card-etoms"><div class="body">' +
            '<div class="section-title mt-0">' + (editing ? 'Sửa livestream' : 'Thêm livestream') + '</div>' +
            '<form id="stream-form" novalidate>' +
            '<div class="field"><label for="sf-url">Link livestream <span class="req">*</span></label>' +
            '<input id="sf-url" name="StreamUrl" class="input" maxlength="500" placeholder="Link của bất kỳ nền tảng nào: YouTube, Facebook, TikTok, Twitch, Kick..." value="' + esc(editing ? editing.streamUrl : '') + '" />' +
            '<div class="hint" id="sf-platform"></div><span class="field-error" data-field-error="StreamUrl"></span></div>' +
            '<div class="field"><label for="sf-title">Tiêu đề</label>' +
            '<input id="sf-title" name="Title" class="input" maxlength="200" placeholder="Ví dụ: Bán kết 1 — Rồng Xanh vs Bạch Hổ" value="' + esc(editing ? editing.title : '') + '" />' +
            '<span class="field-error" data-field-error="Title"></span></div>' +
            '<div class="row g-3"><div class="col-12 col-md-8"><div class="field mb-0"><label for="sf-match">Gán vào trận</label>' +
            '<select id="sf-match" name="MatchId" class="select">' + matchOptions + '</select><span class="field-error" data-field-error="MatchId"></span></div></div>' +
            '<div class="col-12 col-md-4"><div class="field mb-0"><label for="sf-status">Trạng thái</label><select id="sf-status" name="Status" class="select">' +
            [['SCHEDULED', 'Sắp phát'], ['LIVE', 'Đang phát'], ['ENDED', 'Đã kết thúc']].map(function (o) {
                return '<option value="' + o[0] + '"' + (o[0] === status ? ' selected' : '') + '>' + o[1] + '</option>';
            }).join('') + '</select><span class="field-error" data-field-error="Status"></span></div></div></div>' +
            '<div class="btn-row mt-3"><button type="submit" class="btn btn-primary">' + (editing ? 'Lưu thay đổi' : 'Thêm livestream') + '</button>' +
            (editing ? '<button type="button" class="btn btn-ghost" id="sf-cancel">Hủy sửa</button>' : '') + '</div>' +
            '</form></div></section>';
    }

    function listHtml() {
        if (!data.streams.length)
            return '<section class="card-etoms"><div class="empty"><h3>Chưa có livestream</h3><p>Thêm link đầu tiên ở khung bên cạnh.</p></div></section>';
        return '<section class="card-etoms"><div class="body"><div class="section-title mt-0">Danh sách (' + data.streams.length + ')</div>' +
            '<div class="d-grid gap-2">' + data.streams.map(function (s) {
                return '<div class="member-card stream-row' + (s.livestreamId === editingId ? ' editing' : '') + '">' +
                    '<div class="grow min-w-0"><div class="nm text-truncate">' + esc(s.title || s.platformLabel) + '</div>' +
                    '<div class="rl text-truncate">' + esc(s.platformLabel) + ' · ' + esc(s.matchLabel || 'Livestream chung của giải') + '</div>' +
                    '<a class="hint text-truncate d-block" href="' + esc(s.streamUrl) + '" target="_blank" rel="noopener noreferrer">' + esc(s.streamUrl) + '</a></div>' +
                    '<div class="d-flex flex-wrap align-items-center gap-2 justify-content-end">' + LivePlayer.streamBadge(s) +
                    (s.embedUrl ? '' : '<span class="badge badge-gold" title="' + esc(s.platformLabel) + ' không cho phát nhúng link này; người xem bấm nút để mở sang ' + esc(s.platformLabel) + '">Xem trên ' + esc(s.platformLabel) + '</span>') +
                    (data.tournamentStatus === 'CANCELLED' ? '' : '<button type="button" class="btn btn-ghost btn-sm" data-edit="' + s.livestreamId + '">Sửa</button>') +
                    '<button type="button" class="btn btn-ghost btn-sm text-danger-etoms" data-delete="' + s.livestreamId + '">Xóa</button></div></div>';
            }).join('') + '</div>' +
            '<a class="btn btn-ghost mt-3" href="/tournament/live.html?id=' + tournamentId + '">Xem trang Trực tiếp công khai</a></div></section>';
    }

    function render() {
        var cancelled = data.tournamentStatus === 'CANCELLED';
        document.getElementById('stream-root').innerHTML =
            (cancelled ? '<div class="alert alert-danger">Giải đã hủy nên không thêm hoặc sửa livestream được. Bạn vẫn có thể xóa link cũ.</div>' : '') +
            '<div class="d-flex flex-column gap-3">' + (cancelled ? '' : formHtml()) +
            listHtml() + '</div>';
        wireForm();
    }

    function wireForm() {
        var form = document.getElementById('stream-form');
        if (!form) return;
        var hint = document.getElementById('sf-platform');
        function updateHint() { var p = platformOf(form.StreamUrl.value.trim()); hint.textContent = p ? 'Nền tảng: ' + p : ''; }
        form.StreamUrl.addEventListener('input', updateHint);
        updateHint();
        var cancel = document.getElementById('sf-cancel');
        if (cancel) cancel.addEventListener('click', function () { editingId = null; render(); });

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);
            var errors = validate(form);
            if (Object.keys(errors).length) { Ui.applyFieldErrors(form, errors); return; }
            var body = {
                StreamUrl: form.StreamUrl.value.trim(),
                Title: form.Title.value.trim() || null,
                MatchId: form.MatchId.value ? parseInt(form.MatchId.value, 10) : null,
                Status: form.Status.value
            };
            var btn = form.querySelector('[type=submit]');
            btn.disabled = true;
            try {
                var url = '/api/tournaments/' + tournamentId + '/livestreams' + (editingId ? '/' + editingId : '');
                var res = editingId ? await Api.put(url, body) : await Api.post(url, body);
                editingId = null;
                await load();
                Ui.showAlert(flash, 'success', res.message);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.message || 'Không lưu được livestream.');
            } finally { btn.disabled = false; }
        });
    }

    async function load() {
        try {
            data = (await Api.get('/api/tournaments/' + tournamentId + '/livestreams')).data;
        } catch (err) {
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', err.message);
            else showEmpty('Không tải được livestream', err.message);
            return;
        }
        document.getElementById('page-title').textContent = 'Quản lý livestream · ' + data.tournamentName;
        if (!data.canManage) {
            showEmpty('Bạn không quản lý được livestream của giải này', 'Chỉ Ban tổ chức của giải được thêm, sửa hoặc xóa livestream.');
            return;
        }
        render();
    }

    document.addEventListener('DOMContentLoaded', async function () {
        flash = document.getElementById('flash');
        var me = await Ui.requireAuth(['ORGANIZER']);
        if (!me) return;
        tournamentId = Ui.qsInt('id');
        if (!tournamentId || tournamentId < 1) { showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.'); return; }

        document.getElementById('stream-root').addEventListener('click', async function (e) {
            var edit = e.target.closest('[data-edit]');
            if (edit) {
                editingId = parseInt(edit.dataset.edit, 10);
                render();
                var f = document.getElementById('stream-form');
                if (f) { f.scrollIntoView({ behavior: 'smooth', block: 'start' }); f.StreamUrl.focus(); }
                return;
            }
            var del = e.target.closest('[data-delete]');
            if (!del) return;
            var id = parseInt(del.dataset.delete, 10);
            var s = data.streams.find(function (x) { return x.livestreamId === id; });
            if (!await window.etomsConfirm('Xóa livestream "' + (s && (s.title || s.platformLabel)) + '"? Người xem sẽ không thấy link này nữa.')) return;
            try {
                var res = await Api.del('/api/tournaments/' + tournamentId + '/livestreams/' + id);
                if (editingId === id) editingId = null;
                await load();
                Ui.showAlert(flash, 'success', res.message);
            } catch (err) {
                Ui.showAlert(flash, 'danger', err.message || 'Không xóa được livestream.');
            }
        });
        await load();
    });
})();
