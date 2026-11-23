(function () {
    var accountId;
    var flash;
    var linkedGameAccounts = [];

    function showError(container, error, fallback) {
        Ui.showAlert(container, 'danger', (error && error.message) || fallback);
    }

    function renderList(root, items, emptyText, renderer) {
        if (!items || !items.length) {
            root.innerHTML = '<div class="empty"><p>' + Ui.escapeHtml(emptyText) + '</p></div>';
            return;
        }
        // Lịch sử trận có thể rất dài: ô tìm kiếm + phân trang 10 mục/trang.
        Ui.listPager(root, {
            items: items, render: renderer, pageSize: 10, emptyText: emptyText,
            text: function (item) { return Object.keys(item).map(function (k) { var v = item[k]; return v !== null && typeof v !== 'object' ? v : ''; }).join(' '); },
            placeholder: 'Tìm theo tên giải, đội...'
        });
    }

    // Ảnh đại diện hiện tại: "/uploads/avatars/..." (tải từ máy) hoặc link ngoài; null = không có ảnh.
    var currentAvatar = null;
    var AVATAR_PREFIX = '/uploads/avatars/';

    function avatarHtml(url, name) {
        var safe = Ui.safeUrl(url);
        return safe ? '<img src="' + Ui.escapeHtml(safe) + '" alt="" />'
            : Ui.escapeHtml((name || 'P').trim().slice(0, 1).toUpperCase());
    }

    function renderAvatar(url) {
        var name = document.getElementById('profile-full-name').value;
        document.getElementById('profile-avatar-preview').innerHTML = avatarHtml(url, name);
        document.getElementById('profile-nav-avatar').innerHTML = avatarHtml(url, name);
        // Nút căn chỉnh lại chỉ hiện khi đang có ảnh.
        var recrop = document.getElementById('profile-avatar-recrop');
        if (recrop) recrop.hidden = !url;
    }

    // Trình duyệt tải thử ảnh: link không phải ảnh (vd trang tìm kiếm Google) hoặc chết thì báo lỗi.
    function imageLoads(url) {
        return new Promise(function (resolve) {
            var img = new Image();
            var timer = setTimeout(function () { resolve(false); }, 8000);
            img.onload = function () { clearTimeout(timer); resolve(img.naturalWidth > 0); };
            img.onerror = function () { clearTimeout(timer); resolve(false); };
            img.src = url;
        });
    }

    function readImageSize(file) {
        return new Promise(function (resolve) {
            var url = URL.createObjectURL(file);
            var img = new Image();
            img.onload = function () { URL.revokeObjectURL(url); resolve({ w: img.naturalWidth, h: img.naturalHeight }); };
            img.onerror = function () { URL.revokeObjectURL(url); resolve(null); };
            img.src = url;
        });
    }

    // ===== Cắt / căn khung ảnh đại diện (PRF-01) =====
    // Nguồn: Blob ảnh (từ máy, từ data:image, hoặc ảnh máy chủ tải hộ từ liên kết). Ảnh luôn phủ kín khung vuông;
    // người dùng kéo để căn, phóng to 1–4 lần; xuất JPEG 512×512 rồi tải lên như ảnh từ máy.
    var MAX_SOURCE_BYTES = 10 * 1024 * 1024;
    var OUTPUT_SIZE = 512;
    var crop = { img: null, url: null, zoom: 1, x: 0, y: 0, size: 300 };

    function cropScale() { return crop.size / Math.min(crop.img.naturalWidth, crop.img.naturalHeight) * crop.zoom; }

    function clampCrop() {
        var s = cropScale();
        var w = crop.img.naturalWidth * s, h = crop.img.naturalHeight * s;
        crop.x = Math.min(0, Math.max(crop.size - w, crop.x));
        crop.y = Math.min(0, Math.max(crop.size - h, crop.y));
    }

    function drawCrop() {
        var canvas = document.getElementById('crop-canvas');
        var ctx = canvas.getContext('2d');
        var s = cropScale();
        ctx.clearRect(0, 0, crop.size, crop.size);
        ctx.drawImage(crop.img, crop.x, crop.y, crop.img.naturalWidth * s, crop.img.naturalHeight * s);
        // Phủ tối ngoài vòng tròn để thấy ảnh đại diện sẽ trông ra sao.
        ctx.save();
        ctx.fillStyle = 'rgba(0,0,0,.55)';
        ctx.beginPath();
        ctx.rect(0, 0, crop.size, crop.size);
        ctx.arc(crop.size / 2, crop.size / 2, crop.size / 2 - 2, 0, Math.PI * 2, true);
        ctx.fill('evenodd');
        ctx.strokeStyle = 'rgba(255,255,255,.85)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(crop.size / 2, crop.size / 2, crop.size / 2 - 2, 0, Math.PI * 2);
        ctx.stroke();
        ctx.restore();
    }

    // Phóng to quanh một điểm (mặc định tâm khung) để điểm đó đứng yên.
    function setZoom(z, cx, cy) {
        var before = cropScale();
        crop.zoom = Math.min(4, Math.max(1, z));
        var after = cropScale();
        cx = cx == null ? crop.size / 2 : cx;
        cy = cy == null ? crop.size / 2 : cy;
        crop.x = cx - (cx - crop.x) * (after / before);
        crop.y = cy - (cy - crop.y) * (after / before);
        clampCrop();
        document.getElementById('crop-zoom').value = String(crop.zoom);
        drawCrop();
    }

    function resetCrop() {
        crop.zoom = 1;
        var s = cropScale();
        crop.x = (crop.size - crop.img.naturalWidth * s) / 2;
        crop.y = (crop.size - crop.img.naturalHeight * s) / 2;
        document.getElementById('crop-zoom').value = '1';
        drawCrop();
    }

    function loadImageFromBlob(blob) {
        return new Promise(function (resolve) {
            var url = URL.createObjectURL(blob);
            var img = new Image();
            img.onload = function () { resolve({ img: img, url: url }); };
            img.onerror = function () { URL.revokeObjectURL(url); resolve(null); };
            img.src = url;
        });
    }

    // Kiểm tra nguồn ảnh rồi mở hộp thoại cắt. fieldKey: ô hiển thị lỗi (AvatarFile hoặc AvatarUrl).
    async function openCropper(blob, fieldKey) {
        var form = document.getElementById('profile-form');
        Ui.clearFieldErrors(form);
        var errors = {};
        if (!blob || !blob.size) errors[fieldKey] = 'Ảnh trống hoặc không đọc được.';
        else if (blob.size > MAX_SOURCE_BYTES) errors[fieldKey] = 'Ảnh gốc tối đa 10 MB (ảnh này ' + (blob.size / 1048576).toFixed(1) + ' MB).';
        else if (blob.type && blob.type.indexOf('image/') !== 0) errors[fieldKey] = 'Tệp này không phải ảnh.';
        if (errors[fieldKey]) { Ui.applyFieldErrors(form, errors); return; }

        var loaded = await loadImageFromBlob(blob);
        if (!loaded) { errors[fieldKey] = 'Trình duyệt không đọc được ảnh này — hãy thử ảnh JPG hoặc PNG khác.'; Ui.applyFieldErrors(form, errors); return; }
        if (loaded.img.naturalWidth < 64 || loaded.img.naturalHeight < 64) {
            URL.revokeObjectURL(loaded.url);
            errors[fieldKey] = 'Ảnh quá nhỏ (' + loaded.img.naturalWidth + '×' + loaded.img.naturalHeight + ' px) — cần ít nhất 64×64 px.';
            Ui.applyFieldErrors(form, errors);
            return;
        }
        if (crop.url) URL.revokeObjectURL(crop.url);
        crop.img = loaded.img;
        crop.url = loaded.url;
        var dialog = document.getElementById('avatar-crop-dialog');
        crop.size = Math.max(200, Math.min(300, window.innerWidth - 80));
        var canvas = document.getElementById('crop-canvas');
        canvas.width = canvas.height = crop.size;
        canvas.style.width = canvas.style.height = crop.size + 'px';
        resetCrop();
        if (typeof dialog.showModal === 'function') dialog.showModal(); else dialog.setAttribute('open', '');
        document.getElementById('crop-stage').focus();
    }

    function closeCropper() {
        var dialog = document.getElementById('avatar-crop-dialog');
        if (dialog.open) { if (typeof dialog.close === 'function') dialog.close(); else dialog.removeAttribute('open'); }
        if (crop.url) { URL.revokeObjectURL(crop.url); crop.url = null; }
        crop.img = null;
    }

    function exportCrop() {
        return new Promise(function (resolve) {
            var out = document.createElement('canvas');
            out.width = out.height = OUTPUT_SIZE;
            var ctx = out.getContext('2d');
            ctx.fillStyle = '#ffffff';                       // ảnh PNG trong suốt -> nền trắng khi sang JPEG
            ctx.fillRect(0, 0, OUTPUT_SIZE, OUTPUT_SIZE);
            var s = cropScale();
            ctx.imageSmoothingQuality = 'high';
            ctx.drawImage(crop.img, -crop.x / s, -crop.y / s, crop.size / s, crop.size / s, 0, 0, OUTPUT_SIZE, OUTPUT_SIZE);
            out.toBlob(function (blob) { resolve(blob); }, 'image/jpeg', 0.9);
        });
    }

    function wireCropper() {
        var stage = document.getElementById('crop-stage');
        var drag = null;
        stage.addEventListener('pointerdown', function (e) {
            if (!crop.img) return;
            drag = { id: e.pointerId, x: e.clientX, y: e.clientY, ox: crop.x, oy: crop.y };
            stage.setPointerCapture(e.pointerId);
        });
        stage.addEventListener('pointermove', function (e) {
            if (!drag || drag.id !== e.pointerId) return;
            crop.x = drag.ox + (e.clientX - drag.x);
            crop.y = drag.oy + (e.clientY - drag.y);
            clampCrop();
            drawCrop();
        });
        ['pointerup', 'pointercancel'].forEach(function (t) { stage.addEventListener(t, function () { drag = null; }); });
        stage.addEventListener('wheel', function (e) {
            if (!crop.img) return;
            e.preventDefault();
            var rect = stage.querySelector('canvas').getBoundingClientRect();
            setZoom(crop.zoom * (e.deltaY < 0 ? 1.1 : 1 / 1.1), e.clientX - rect.left, e.clientY - rect.top);
        }, { passive: false });
        stage.addEventListener('keydown', function (e) {
            if (!crop.img) return;
            var step = e.shiftKey ? 20 : 5;
            if (e.key === 'ArrowLeft') crop.x += step;
            else if (e.key === 'ArrowRight') crop.x -= step;
            else if (e.key === 'ArrowUp') crop.y += step;
            else if (e.key === 'ArrowDown') crop.y -= step;
            else if (e.key === '+' || e.key === '=') { setZoom(crop.zoom * 1.1); e.preventDefault(); return; }
            else if (e.key === '-') { setZoom(crop.zoom / 1.1); e.preventDefault(); return; }
            else return;
            e.preventDefault();
            clampCrop();
            drawCrop();
        });
        document.getElementById('crop-zoom').addEventListener('input', function (e) { setZoom(parseFloat(e.target.value)); });
        document.getElementById('crop-reset').addEventListener('click', resetCrop);
        document.querySelectorAll('[data-crop-cancel]').forEach(function (b) { b.addEventListener('click', closeCropper); });
        document.getElementById('avatar-crop-dialog').addEventListener('cancel', function () { closeCropper(); });
        document.getElementById('crop-save').addEventListener('click', async function () {
            var btn = this;
            btn.disabled = true;
            try {
                var blob = await exportCrop();
                if (!blob) { Ui.showAlert(flash, 'danger', 'Không xuất được ảnh — hãy thử lại.'); return; }
                closeCropper();
                await uploadAvatar(new File([blob], 'avatar.jpg', { type: 'image/jpeg' }), document.getElementById('profile-form'));
                document.getElementById('profile-avatar').value = '';
            } finally {
                btn.disabled = false;
            }
        });
    }

    // Dán liên kết: data:image/... đọc thẳng trong trình duyệt; liên kết http(s) (mọi đuôi, kể cả Google imgres)
    // để máy chủ tải hộ — trình duyệt không tự đọc được ảnh khác nguồn vào canvas để cắt.
    async function useAvatarLink() {
        var input = document.getElementById('profile-avatar');
        var form = document.getElementById('profile-form');
        var raw = input.value.trim();
        Ui.clearFieldErrors(form);
        if (!raw) { Ui.applyFieldErrors(form, { AvatarUrl: 'Hãy dán liên kết ảnh trước.' }); input.focus(); return; }
        var btn = document.getElementById('profile-avatar-use-link');
        btn.disabled = true;
        var old = btn.textContent;
        btn.textContent = 'Đang tải ảnh…';
        try {
            if (/^data:image\//i.test(raw)) {
                var blob = null;
                try { blob = await (await fetch(raw)).blob(); } catch (e) { blob = null; }
                if (!blob) { Ui.applyFieldErrors(form, { AvatarUrl: 'Dữ liệu ảnh dán vào bị hỏng hoặc không đầy đủ.' }); return; }
                await openCropper(blob, 'AvatarUrl');
                return;
            }
            if (!/^https?:\/\//i.test(raw)) {
                Ui.applyFieldErrors(form, { AvatarUrl: 'Liên kết phải bắt đầu bằng http:// hoặc https:// (hoặc dán trực tiếp ảnh dạng data:image).' });
                return;
            }
            var res = await Api.fetchRaw('/api/profiles/' + accountId + '/avatar/fetch', {
                method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ url: raw })
            });
            if (!res.ok) {
                var err = null;
                try { err = await res.json(); } catch (e) { err = null; }
                Ui.applyFieldErrors(form, { AvatarUrl: (err && err.message) || 'Không tải được ảnh từ liên kết này.' });
                return;
            }
            await openCropper(await res.blob(), 'AvatarUrl');
        } catch (error) {
            Ui.applyFieldErrors(form, { AvatarUrl: 'Không tải được ảnh từ liên kết này — hãy thử lại hoặc chọn ảnh từ máy.' });
        } finally {
            btn.disabled = false;
            btn.textContent = old;
        }
    }

    async function uploadAvatar(file, form) {
        Ui.clearFieldErrors(form);
        var errors = {};
        if (['image/jpeg', 'image/png'].indexOf(file.type) < 0) errors.AvatarFile = 'Chỉ nhận ảnh JPG hoặc PNG.';
        else if (file.size > 2 * 1024 * 1024) errors.AvatarFile = 'Ảnh tối đa 2 MB (ảnh của bạn ' + (file.size / 1048576).toFixed(1) + ' MB).';
        else {
            var size = await readImageSize(file);
            if (!size) errors.AvatarFile = 'Không đọc được ảnh — tệp có thể bị hỏng.';
            else if (size.w < 64 || size.h < 64 || size.w > 4096 || size.h > 4096)
                errors.AvatarFile = 'Kích thước ảnh phải từ 64×64 đến 4096×4096 px (ảnh của bạn ' + size.w + '×' + size.h + ' px).';
        }
        if (errors.AvatarFile) { Ui.applyFieldErrors(form, errors); return; }

        var body = new FormData();
        body.append('file', file);
        try {
            var result = await Api.postForm('/api/profiles/' + accountId + '/avatar', body);
            currentAvatar = result.data.avatarUrl;
            document.getElementById('profile-avatar').value = '';
            renderAvatar(currentAvatar);
            Ui.showAlert(flash, 'success', result.message || 'Đã cập nhật ảnh đại diện.');
            loadPublicProfile();
        } catch (error) {
            Ui.applyFieldErrors(form, error.fieldErrors);
            showError(flash, error, 'Không tải được ảnh lên.');
        }
    }

    // Cùng luật với ProfileService.UpdateProfileAsync.
    function validateProfile(form, body) {
        var errors = {};
        var name = (body.FullName || '').trim();
        if (name.length < 2 || name.length > 100) errors.FullName = 'Tên hiển thị phải từ 2 đến 100 ký tự.';
        if (body.Phone && !/^0\d{9}$/.test(body.Phone)) errors.Phone = 'Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0.';
        if (body.DateOfBirth) {
            var dob = new Date(body.DateOfBirth + 'T00:00:00'), now = new Date();
            var age = now.getFullYear() - dob.getFullYear() - ((now.getMonth() < dob.getMonth() || (now.getMonth() === dob.getMonth() && now.getDate() < dob.getDate())) ? 1 : 0);
            if (isNaN(dob.getTime()) || dob.getFullYear() < 1900) errors.DateOfBirth = 'Ngày sinh không hợp lệ.';
            else if (dob > now) errors.DateOfBirth = 'Ngày sinh không được ở tương lai.';
            else if (age < 18) errors.DateOfBirth = 'Bạn phải đủ 18 tuổi (cùng quy định với lúc đăng ký).';
        }
        if (body.SocialLinks) {
            var lines = body.SocialLinks.split('\n').map(function (l) { return l.trim(); }).filter(Boolean);
            var bad = lines.filter(function (l) { return !/^https?:\/\/\S+\.\S+/i.test(l); })[0];
            if (lines.length > 10) errors.SocialLinks = 'Tối đa 10 liên kết mạng xã hội.';
            else if (bad) errors.SocialLinks = 'Liên kết "' + bad.slice(0, 40) + '" không hợp lệ — mỗi dòng phải bắt đầu bằng http:// hoặc https://.';
        }
        if (body.AvatarUrl && body.AvatarUrl.indexOf(AVATAR_PREFIX) !== 0 && !/^https?:\/\/\S+\.\S+/i.test(body.AvatarUrl))
            errors.AvatarUrl = 'Ảnh đại diện phải là liên kết bắt đầu bằng http:// hoặc https://, hoặc chọn ảnh từ máy.';
        Ui.applyFieldErrors(form, errors);
        return Object.keys(errors).length === 0;
    }

    async function loadProfile() {
        var response = await Api.get('/api/profiles/' + accountId);
        var profile = response.data;
        document.getElementById('profile-email').textContent = profile.email || '';
        document.getElementById('profile-nav-name').textContent = profile.fullName || 'Người chơi';
        document.getElementById('profile-full-name').value = profile.fullName || '';
        currentAvatar = profile.avatarUrl || null;
        renderAvatar(currentAvatar);
        // Ô link chỉ hiện link ngoài; ảnh đã tải lên thì để trống (giữ nguyên khi lưu).
        document.getElementById('profile-avatar').value = '';
        document.getElementById('profile-dob').value = profile.dateOfBirth || '';
        document.getElementById('profile-phone').value = profile.phone || '';
        document.getElementById('profile-social').value = profile.socialLinks || '';
    }

    async function loadGames() {
        var [profileResponse, summary] = await Promise.all([
            Api.get('/api/profiles/' + accountId + '/public'),
            Api.get('/api/home/summary')
        ]);
        var profile = profileResponse.data;
        var select = document.getElementById('game-id');
        var gameOptions = summary.games || [];
        linkedGameAccounts = profile.gameAccounts || [];
        select.innerHTML = '<option value="">Chọn tựa game</option>' + gameOptions.map(function (game) {
            return '<option value="' + Number(game.gameId) + '">' + Ui.escapeHtml(game.title) + '</option>';
        }).join('');
        updateGameAccountForm();
        renderList(document.getElementById('profile-games'), linkedGameAccounts, 'Chưa liên kết tài khoản game.', function (game) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(game.gameTitle) + '</div><div class="item-sub">' + Ui.escapeHtml(game.inGameName) + '</div></div></div>';
        });
        return profile;
    }

    function updateGameAccountForm() {
        var select = document.getElementById('game-id');
        var input = document.getElementById('in-game-name');
        var existing = linkedGameAccounts.find(function (item) {
            return Number(item.gameId) === Number(select.value);
        });
        input.value = existing ? existing.inGameName : '';
        document.getElementById('game-submit').textContent = existing ? 'Cập nhật tên game' : 'Thêm tên game';
        document.getElementById('game-account-hint').textContent = existing
            ? 'Đã có tên liên kết cho tựa game này. Lưu sẽ cập nhật tên hiện tại, không tạo liên kết trùng.'
            : 'Mỗi tựa game chỉ có một tên liên kết trong phiên bản hiện tại.';
    }

    async function loadIdentityDocuments() {
        var response = await Api.get('/api/profiles/' + accountId + '/identity-documents');
        var documents = response.data;
        renderList(document.getElementById('identity-documents'), documents, 'Chưa có giấy tờ xác minh.', function (doc) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(doc.credentialTypeLabel || doc.credentialType) + '</div><div class="item-sub">Đã gửi ' + Ui.escapeHtml(Ui.fmtDateTime(doc.createdAt)) + '</div></div><a class="btn btn-ghost btn-sm" href="/api/profiles/' + accountId + '/identity-documents/' + Number(doc.credentialId) + '/file" data-private-file="giay-to-dinh-danh">Xem bản đã gửi</a></div>';
        });
    }

    async function loadPreferences() {
        var response = await Api.get('/api/profiles/' + accountId + '/notification-preferences');
        var preferences = response.data;
        document.querySelector('[name="NotifyInApp"]').checked = !!preferences.notifyInApp;
        document.querySelector('[name="NotifyEmail"]').checked = !!preferences.notifyEmail;
    }

    async function loadPublicProfile() {
        var response = await Api.get('/api/profiles/' + accountId + '/public');
        var profile = response.data;
        var root = document.getElementById('public-profile');
        var gameAccounts = (profile.gameAccounts || []).map(function (item) {
            return '<span class="badge badge-soft">' + Ui.escapeHtml(item.gameTitle) + ': ' + Ui.escapeHtml(item.inGameName) + '</span>';
        }).join(' ');
        var teams = (profile.teams || []).map(function (item) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(item.teamName) + '</div><div class="item-sub">' + Ui.escapeHtml(item.gameTitle) + '</div></div></div>';
        }).join('');
        var titles = (profile.titles || []).map(function (item) {
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(item.tournamentName) + '</div><div class="item-sub">' + Ui.escapeHtml(item.placementLabel || item.placement) + '</div></div></div>';
        }).join('');
        var avatarUrl = Ui.safeUrl(profile.avatarUrl);
        root.innerHTML = '<div class="d-flex align-items-center gap-3 mb-3"><div class="org-mark">' +
            (avatarUrl ? '<img src="' + Ui.escapeHtml(avatarUrl) + '" alt="" />' : Ui.escapeHtml((profile.fullName || '?').slice(0, 1))) +
            '</div><div><h3 class="mb-1">' + Ui.escapeHtml(profile.fullName) + '</h3><div class="muted-copy">Mã người chơi #' + Number(profile.accountId) + '</div></div></div>' +
            '<p><a class="btn btn-ghost btn-sm" href="/profile/public.html?id=' + Number(profile.accountId) + '">Mở trang hồ sơ công khai</a></p>' +
            '<div class="d-flex flex-wrap gap-2 mb-3">' + (gameAccounts || '<span class="muted-copy">Chưa có tài khoản game công khai.</span>') + '</div>' +
            '<div class="row g-3"><div class="col-md-6"><h3 class="section-title">Đội tuyển</h3>' + (teams ? '<div class="feature-list">' + teams + '</div>' : '<p class="muted-copy">Chưa tham gia đội tuyển.</p>') + '</div>' +
            '<div class="col-md-6"><h3 class="section-title">Thành tích</h3>' + (titles ? '<div class="feature-list">' + titles + '</div>' : '<p class="muted-copy">Chưa có thành tích.</p>') + '</div></div>';
    }

    async function loadMatchHistory() {
        var response = await Api.get('/api/profiles/' + accountId + '/match-history');
        var matches = response.data;
        renderList(document.getElementById('match-history'), matches, 'Chưa có lịch sử trận đấu.', function (match) {
            var score = match.scoreFor === null || match.scoreAgainst === null ? 'Chưa có kết quả' : match.scoreFor + ' - ' + match.scoreAgainst;
            var schedule = match.scheduledAt ? Ui.fmtDateTime(match.scheduledAt) : 'Chưa xếp lịch';
            return '<div class="feature-list-item"><div><div class="item-title">' + Ui.escapeHtml(match.tournamentName) + ' · ' + Ui.escapeHtml(match.teamName) + '</div><div class="item-sub">' + Ui.escapeHtml(match.slot) + ' · ' + Ui.escapeHtml(schedule) + '</div></div><span class="badge badge-soft">' + Ui.escapeHtml(match.statusLabel || match.status) + ' · ' + Ui.escapeHtml(score) + '</span></div>';
        });
    }

    function wireForms() {
        var form = document.getElementById('profile-form');
        wireCropper();
        document.getElementById('profile-avatar-file').addEventListener('change', function (event) {
            var file = event.target.files && event.target.files[0];
            event.target.value = '';                       // chọn lại cùng tệp vẫn kích hoạt change
            if (file) openCropper(file, 'AvatarFile');
        });
        document.getElementById('profile-avatar-use-link').addEventListener('click', useAvatarLink);
        document.getElementById('profile-avatar').addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); useAvatarLink(); }
        });
        // Căn chỉnh lại ảnh đang dùng: ảnh đã lưu trên ETOMS đọc trực tiếp, link ngoài cũ thì nhờ máy chủ tải hộ.
        document.getElementById('profile-avatar-recrop').addEventListener('click', async function () {
            if (!currentAvatar) return;
            try {
                var res = currentAvatar.indexOf(AVATAR_PREFIX) === 0
                    ? await fetch(currentAvatar, { credentials: 'same-origin' })
                    : await Api.fetchRaw('/api/profiles/' + accountId + '/avatar/fetch', {
                        method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ url: currentAvatar })
                    });
                if (!res.ok) throw new Error();
                await openCropper(await res.blob(), 'AvatarFile');
            } catch (e) {
                Ui.applyFieldErrors(form, { AvatarFile: 'Không mở được ảnh hiện tại để căn chỉnh — hãy chọn lại ảnh.' });
            }
        });
        document.getElementById('profile-avatar-clear').addEventListener('click', function () {
            currentAvatar = null;
            document.getElementById('profile-avatar').value = '';
            renderAvatar(null);
            Ui.showAlert(flash, 'success', 'Đã bỏ ảnh — bấm "Lưu thông tin" để áp dụng.');
        });

        form.addEventListener('submit', async function (event) {
            event.preventDefault();
            Ui.clearFieldErrors(form);
            Ui.clearAlert(flash);
            var submit = form.querySelector('button[type="submit"]');
            try {
                var data = new FormData(form);
                // Liên kết ảnh phải qua bước "Dùng ảnh này" (tải về + căn khung) — không lưu link thô nữa.
                if (document.getElementById('profile-avatar').value.trim()) {
                    Ui.applyFieldErrors(form, { AvatarUrl: 'Bấm "Dùng ảnh này" để tải và căn khung ảnh trước, hoặc xóa liên kết trong ô.' });
                    showError(flash, null, 'Vui lòng sửa các ô được đánh dấu.');
                    return;
                }
                var linked = '';
                var body = {
                    FullName: (data.get('FullName') || '').trim(),
                    AvatarUrl: currentAvatar || null,
                    DateOfBirth: data.get('DateOfBirth') || null,
                    Phone: (data.get('Phone') || '').trim() || null,
                    SocialLinks: (data.get('SocialLinks') || '').trim() || null
                };
                if (!validateProfile(form, body)) { showError(flash, null, 'Vui lòng sửa các ô được đánh dấu.'); return; }
                submit.disabled = true;
                if (linked && !(await imageLoads(linked))) {
                    Ui.applyFieldErrors(form, { AvatarUrl: 'Không tải được ảnh từ liên kết này — hãy dùng liên kết trực tiếp tới tệp ảnh (thường kết thúc bằng .jpg, .png) hoặc chọn ảnh từ máy.' });
                    showError(flash, null, 'Vui lòng sửa các ô được đánh dấu.');
                    return;
                }
                var result = await Api.put('/api/profiles/' + accountId, body);
                Ui.showAlert(flash, 'success', result.message || 'Đã cập nhật hồ sơ.');
                await Promise.all([loadProfile(), loadPublicProfile()]);
            } catch (error) {
                Ui.applyFieldErrors(form, error.fieldErrors);
                showError(flash, error, 'Không thể cập nhật hồ sơ.');
            } finally {
                submit.disabled = false;
            }
        });

        var gameForm = document.getElementById('game-form');
        document.getElementById('game-id').addEventListener('change', updateGameAccountForm);
        gameForm.addEventListener('submit', async function (event) {
            event.preventDefault();
            Ui.clearFieldErrors(gameForm);
            var gameId = Number(document.getElementById('game-id').value);
            if (!gameId) {
                Ui.applyFieldErrors(gameForm, { GameId: 'Vui lòng chọn tựa game.' });
                return;
            }
            var ign = document.getElementById('in-game-name').value.trim();
            if (ign.length < 2 || ign.length > 150) {
                Ui.applyFieldErrors(gameForm, { InGameName: 'Tên trong game phải từ 2 đến 150 ký tự.' });
                return;
            }
            try {
                var result = await Api.put('/api/profiles/' + accountId + '/games/' + gameId, {
                    InGameName: ign
                });
                Ui.showAlert(flash, 'success', result.message || 'Đã lưu tài khoản game.');
                await loadGames();
                await loadPublicProfile();
            } catch (error) {
                Ui.applyFieldErrors(gameForm, error.fieldErrors);
                showError(flash, error, 'Không thể lưu tài khoản game.');
            }
        });

        var identityForm = document.getElementById('identity-form');
        identityForm.addEventListener('submit', async function (event) {
            event.preventDefault();
            Ui.clearFieldErrors(identityForm);
            var file = document.getElementById('identity-file').files[0];
            if (!file) {
                Ui.applyFieldErrors(identityForm, { file: 'Chọn tệp cần tải lên.' });
                return;
            }
            var allowedExtensions = ['.jpg', '.jpeg', '.png', '.pdf'];
            var extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
            if (!allowedExtensions.includes(extension)) {
                Ui.applyFieldErrors(identityForm, { file: 'Chỉ chấp nhận tệp JPG, PNG hoặc PDF.' });
                return;
            }
            if (file.size === 0 || file.size > 10 * 1024 * 1024) {
                Ui.applyFieldErrors(identityForm, { file: 'Tệp phải có dung lượng lớn hơn 0 và không vượt quá 10 MB.' });
                return;
            }
            var body = new FormData();
            body.append('file', file);
            try {
                var result = await Api.postForm('/api/profiles/' + accountId + '/identity-documents', body);
                Ui.showAlert(flash, 'success', result.message || 'Đã tải giấy tờ lên.');
                identityForm.reset();
                await loadIdentityDocuments();
            } catch (error) {
                Ui.applyFieldErrors(identityForm, error.fieldErrors);
                showError(flash, error, 'Không thể tải giấy tờ lên.');
            }
        });

        var preferencesForm = document.getElementById('preferences-form');
        preferencesForm.addEventListener('submit', async function (event) {
            event.preventDefault();
            try {
                var result = await Api.put('/api/profiles/' + accountId + '/notification-preferences', {
                    NotifyInApp: document.querySelector('[name="NotifyInApp"]').checked,
                    NotifyEmail: document.querySelector('[name="NotifyEmail"]').checked
                });
                Ui.showAlert(flash, 'success', result.message || 'Đã lưu tùy chọn thông báo.');
            } catch (error) {
                showError(flash, error, 'Không thể lưu tùy chọn.');
            }
        });
    }

    function wireSectionNavigation() {
        var links = Array.from(document.querySelectorAll('.profile-section-nav a'));
        var sections = links.map(function (link) {
            return document.querySelector(link.getAttribute('href'));
        }).filter(Boolean);
        if (!('IntersectionObserver' in window)) return;

        var observer = new IntersectionObserver(function (entries) {
            var visible = entries.filter(function (entry) { return entry.isIntersecting; })
                .sort(function (left, right) { return left.boundingClientRect.top - right.boundingClientRect.top; })[0];
            if (!visible) return;
            links.forEach(function (link) {
                var active = link.getAttribute('href') === '#' + visible.target.id;
                link.classList.toggle('active', active);
                if (active) link.setAttribute('aria-current', 'location');
                else link.removeAttribute('aria-current');
            });
        }, { rootMargin: '-18% 0px -68% 0px' });

        sections.forEach(function (section) { observer.observe(section); });
    }

    // ===== UC-43: xin làm Ban tổ chức (chỉ tài khoản tuyển thủ) =====
    function fmtTime(iso) {
        if (!iso) return '';
        return Ui.fmtDateTime(iso);
    }

    function renderOrganizerRequest(dto) {
        var section = document.getElementById('organizer-request');
        var nav = document.getElementById('orq-nav');
        if (!dto || dto.role !== 'PLAYER') { section.hidden = true; if (nav) nav.hidden = true; return; }
        section.hidden = false;
        if (nav) nav.hidden = false;
        var badge = document.getElementById('orq-status');
        badge.textContent = dto.statusLabel;
        badge.className = 'badge ' + (dto.status === 'PENDING' ? 'badge-gold' : dto.status === 'APPROVED' ? 'badge-active' : 'badge-soft');
        var view = document.getElementById('orq-view');
        var form = document.getElementById('orq-form');
        if (dto.status === 'PENDING') {
            view.innerHTML = '<div class="feature-list-item"><div><div class="item-title">Đơn gửi lúc ' + Ui.escapeHtml(fmtTime(dto.requestedAt)) + '</div>' +
                '<div class="item-sub orq-note">' + Ui.escapeHtml(dto.note || '') + '</div>' +
                (dto.proposedEmail ? '<div class="item-sub">Email đề xuất: ' + Ui.escapeHtml(dto.proposedEmail) + '</div>' : '') + '</div>' +
                '<button type="button" class="btn btn-ghost btn-sm" id="orq-cancel">Hủy đơn</button></div>';
            form.hidden = true;
        } else if (dto.status === 'APPROVED') {
            view.innerHTML = '<p class="muted-copy">Bạn đã được cấp tài khoản Ban tổ chức riêng — xem email/thông báo để biết email đăng nhập và mật khẩu tạm. Đăng xuất rồi đăng nhập bằng tài khoản đó để tổ chức giải.</p>';
            form.hidden = true;
        } else {
            view.innerHTML = dto.status === 'REJECTED'
                ? '<p class="muted-copy">Đơn trước đã bị từ chối (lý do trong thông báo). Bạn có thể bổ sung và gửi lại.</p>' : '';
            form.hidden = !dto.canSubmit;
        }
        var cancel = document.getElementById('orq-cancel');
        if (cancel) cancel.addEventListener('click', async function () {
            if (window.etomsConfirm && !(await window.etomsConfirm('Hủy đơn xin làm Ban tổ chức?'))) return;
            cancel.disabled = true;
            try {
                var res = await Api.del('/api/me/organizer-request');
                Ui.showAlert(document.getElementById('orq-flash'), 'success', res.message || 'Đã hủy đơn.');
                renderOrganizerRequest(res.data);
            } catch (err) {
                Ui.showAlert(document.getElementById('orq-flash'), 'danger', err.message);
                cancel.disabled = false;
            }
        });
    }

    // Đơn ứng tuyển vào đội đang chờ duyệt (trang Tìm đội) — xem và rút đơn.
    async function loadApplications() {
        var box = document.getElementById('my-applications');
        var list = await Api.get('/api/Recruitment/my-applications') || [];
        box.innerHTML = list.length ? list.map(function (a) {
            return '<div class="feature-list-item"><div><div class="item-title"><a href="/team/details.html?id=' + Number(a.teamId) + '">' +
                Ui.escapeHtml(a.teamName) + ' [' + Ui.escapeHtml(a.teamTag) + ']</a></div>' +
                '<div class="item-sub">' + Ui.escapeHtml(a.gameTitle) + (a.sentAt ? ' · gửi ' + Ui.escapeHtml(fmtTime(a.sentAt)) : '') + ' · Chờ đội trưởng duyệt</div></div>' +
                '<button type="button" class="btn btn-ghost btn-sm" data-withdraw="' + Number(a.teamMemberId) + '">Rút đơn</button></div>';
        }).join('') : '<p class="muted-copy">Bạn không có đơn ứng tuyển nào đang chờ. <a href="/free-agent/index.html">Tìm đội</a></p>';
        box.querySelectorAll('[data-withdraw]').forEach(function (b) {
            b.addEventListener('click', async function () {
                if (window.etomsConfirm && !(await window.etomsConfirm('Rút đơn ứng tuyển vào đội này?'))) return;
                b.disabled = true;
                try {
                    var res = await Api.post('/api/Recruitment/applications/' + b.dataset.withdraw + '/withdraw');
                    Ui.showAlert(document.getElementById('applications-flash'), 'success', res.message || 'Đã rút đơn.');
                    await loadApplications();
                } catch (err) {
                    Ui.showAlert(document.getElementById('applications-flash'), 'danger', err.message);
                    b.disabled = false;
                }
            });
        });
    }

    async function loadOrganizerRequest() {
        var dto = await Api.get('/api/me/organizer-request');
        renderOrganizerRequest(dto && dto.role ? dto : (dto && dto.data));
    }

    function wireOrganizerRequest() {
        var form = document.getElementById('orq-form');
        var note = document.getElementById('orq-note');
        note.addEventListener('input', function () { document.getElementById('orq-note-count').textContent = note.value.length; });
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearFieldErrors(form);
            var box = document.getElementById('orq-flash');
            Ui.clearAlert(box);
            var body = { Note: note.value.trim(), ProposedEmail: document.getElementById('orq-email').value.trim() || null };
            var errors = {};
            if (body.Note.length < 20 || body.Note.length > 1000) errors.Note = 'Hãy ghi mong muốn / lý do muốn làm Ban tổ chức (20–1000 ký tự).';
            if (body.ProposedEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(body.ProposedEmail)) errors.ProposedEmail = 'Email đề xuất không hợp lệ.';
            if (Object.keys(errors).length) { Ui.applyFieldErrors(form, errors); return; }
            var btn = form.querySelector('button[type="submit"]');
            btn.disabled = true;
            try {
                var res = await Api.post('/api/me/organizer-request', body);
                Ui.showAlert(box, 'success', res.message || 'Đã gửi đơn.');
                form.reset();
                document.getElementById('orq-note-count').textContent = '0';
                renderOrganizerRequest(res.data);
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(box, 'danger', err.message);
            } finally {
                btn.disabled = false;
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        wireSectionNavigation();
        flash = document.getElementById('profile-flash');
        document.getElementById('profile-dob').max = new Date().toISOString().slice(0, 10);
        var me = await Ui.requireAuth();
        if (!me) return;
        accountId = Number(me.userId);
        if (!accountId) {
            showError(flash, null, 'Không xác định được tài khoản đang đăng nhập.');
            return;
        }
        wireForms();
        wireOrganizerRequest();
        // Ban tổ chức / Admin không có đội, không thi đấu: ẩn phần chỉ dành cho tuyển thủ (tên trong game,
        // minh chứng định danh để thi đấu, hồ sơ thi đấu, lịch sử trận).
        var isPlayer = me.role === 'PLAYER';
        if (!isPlayer) {
            ['profile-game-section', 'profile-public', 'profile-history', 'profile-applications'].forEach(function (id) {
                var s = document.getElementById(id); if (s) s.hidden = true;
                var link = document.querySelector('a[href="#' + id + '"]'); if (link) link.hidden = true;
            });
            var identityBlock = document.getElementById('identity-form');
            if (identityBlock) identityBlock.closest('.profile-subsection').hidden = true;
            var privacyTitle = document.getElementById('profile-privacy-title');
            if (privacyTitle) privacyTitle.textContent = 'Thông báo';
            var privacyLink = document.querySelector('a[href="#profile-privacy"]');
            if (privacyLink) privacyLink.textContent = 'Thông báo';
        }
        var sections = isPlayer
            ? [loadProfile, loadGames, loadIdentityDocuments, loadPreferences, loadPublicProfile, loadMatchHistory, loadOrganizerRequest, loadApplications]
            : [loadProfile, loadPreferences, loadOrganizerRequest];
        var results = await Promise.allSettled(sections.map(function (load) { return load(); }));
        var failures = results.filter(function (result) { return result.status === 'rejected'; });
        if (failures.length) {
            showError(flash, failures[0].reason, 'Một số thông tin hồ sơ không tải được.');
        }
    });
})();
