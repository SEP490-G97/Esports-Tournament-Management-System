(function () {
    var games = [];
    var picked = [];          // [{ accountId, fullName, inGameName }]
    var results = [];
    var active = -1;
    var searchTimer = null;
    var searchSeq = 0;

    function esc(s) { return Ui.escapeHtml(s == null ? '' : String(s)); }
    function gameId() { return parseInt(document.getElementById('GameId').value, 10) || 0; }
    function currentGame() { return games.find(function (g) { return g.gameId === gameId(); }) || null; }
    // Đội trưởng chiếm 1 chỗ: số người mời tối đa = chính thức + dự bị − 1 (cùng luật với máy chủ).
    function maxInvites() {
        var g = currentGame();
        return g ? Math.max(0, (g.maxStarters || 1) + (g.maxSubstitutes || 0) - 1) : 0;
    }

    async function populateGames() {
        var select = document.getElementById('GameId');
        try {
            games = await Api.get('/api/teams/games');
            games.forEach(function (g) {
                var opt = document.createElement('option');
                opt.value = g.gameId;
                opt.textContent = g.title;
                select.appendChild(opt);
            });
        } catch (e) {}
    }

    function wireLogoPreview() {
        var input = document.getElementById('logoFile');
        var img = document.getElementById('logoPreview');
        input.addEventListener('change', function () {
            if (input.files && input.files[0]) {
                img.src = URL.createObjectURL(input.files[0]);
                img.style.display = 'block';
            } else {
                img.style.display = 'none';
            }
        });
    }

    function wireDescription() {
        var box = document.getElementById('Description');
        var count = document.getElementById('descCount');
        box.addEventListener('input', function () { count.textContent = box.value.length; });
    }

    // ===== Ô "Thành viên": tìm theo tên người dùng / tên trong game, chọn nhiều người =====
    function renderChips() {
        var root = document.getElementById('memberChips');
        root.innerHTML = picked.map(function (p) {
            return '<span class="member-chip">' + esc(p.fullName) + (p.inGameName ? ' <small>· ' + esc(p.inGameName) + '</small>' : '') +
                '<button type="button" data-remove="' + p.accountId + '" aria-label="Bỏ ' + esc(p.fullName) + '">×</button></span>';
        }).join('');
        root.querySelectorAll('[data-remove]').forEach(function (b) {
            b.addEventListener('click', function () {
                var id = parseInt(b.dataset.remove, 10);
                picked = picked.filter(function (p) { return p.accountId !== id; });
                renderChips();
                document.getElementById('memberSearch').focus();
            });
        });
        var hint = document.getElementById('memberHint');
        var max = maxInvites();
        if (gameId() && max === 0) hint.textContent = 'Tựa game này mỗi đội chỉ có 1 người (chính bạn) nên không cần mời thành viên.';
        else if (gameId() && picked.length) hint.textContent = 'Đã chọn ' + picked.length + '/' + max + ' người. Mỗi người nhận một lời mời và tự bấm chấp nhận.';
        else if (gameId()) hint.textContent = 'Có thể mời tối đa ' + max + ' tuyển thủ (đội trưởng chiếm 1 chỗ). Mỗi người nhận một lời mời và tự bấm chấp nhận.';
    }

    function closeResults() {
        var list = document.getElementById('memberResults');
        list.hidden = true;
        list.innerHTML = '';
        results = [];
        active = -1;
        document.getElementById('memberSearch').setAttribute('aria-expanded', 'false');
    }

    function renderResults(message) {
        var list = document.getElementById('memberResults');
        var input = document.getElementById('memberSearch');
        if (message) {
            list.innerHTML = '<li class="member-empty">' + esc(message) + '</li>';
        } else {
            list.innerHTML = results.map(function (r, i) {
                var chosen = picked.some(function (p) { return p.accountId === r.accountId; });
                var disabled = r.alreadyInTeam || chosen;
                var note = r.alreadyInTeam ? 'Đã có đội ở game này' : chosen ? 'Đã chọn' : '';
                return '<li role="option" id="mr-' + i + '" data-i="' + i + '" class="member-option' + (i === active ? ' active' : '') + (disabled ? ' disabled' : '') + '" aria-disabled="' + disabled + '">' +
                    '<span class="avatar-xs">' + (r.avatarUrl ? '<img src="' + esc(r.avatarUrl) + '" alt="" />' : esc((r.fullName || '?').charAt(0).toUpperCase())) + '</span>' +
                    '<span class="grow"><strong>' + esc(r.fullName) + '</strong>' + (r.inGameName ? ' <small class="text-muted-2">· ' + esc(r.inGameName) + '</small>' : '') +
                    (r.email ? '<small class="member-option-email">' + esc(r.email) + '</small>' : '') + '</span>' +
                    (note ? '<small class="text-muted-2">' + note + '</small>' : '') + '</li>';
            }).join('');
            list.querySelectorAll('[data-i]').forEach(function (li) {
                li.addEventListener('mousedown', function (e) { e.preventDefault(); choose(parseInt(li.dataset.i, 10)); });
            });
        }
        list.hidden = false;
        input.setAttribute('aria-expanded', 'true');
        input.setAttribute('aria-activedescendant', active >= 0 ? 'mr-' + active : '');
    }

    function choose(i) {
        var r = results[i];
        if (!r || r.alreadyInTeam || picked.some(function (p) { return p.accountId === r.accountId; })) return;
        if (picked.length >= maxInvites()) {
            renderResults('Đã đủ ' + maxInvites() + ' người — tựa game này mỗi đội tối đa ' + (maxInvites() + 1) + ' người.');
            return;
        }
        picked.push({ accountId: r.accountId, fullName: r.fullName, inGameName: r.inGameName });
        renderChips();
        var input = document.getElementById('memberSearch');
        input.value = '';
        closeResults();
        input.focus();
    }

    async function search(q) {
        var seq = ++searchSeq;
        try {
            var data = await Api.get('/api/teams/player-search?gameId=' + gameId() + '&q=' + encodeURIComponent(q));
            if (seq !== searchSeq) return;   // bỏ kết quả cũ khi người dùng đã gõ tiếp
            results = Array.isArray(data) ? data : (data.data || []);
            active = results.findIndex(function (r) { return !r.alreadyInTeam; });
            renderResults(results.length ? null : 'Không tìm thấy tuyển thủ phù hợp.');
        } catch (err) {
            if (seq === searchSeq) renderResults(err.message || 'Không tìm được tuyển thủ.');
        }
    }

    function wireMemberPicker() {
        var input = document.getElementById('memberSearch');
        document.getElementById('GameId').addEventListener('change', function () {
            // Đổi tựa game: lời mời gắn với đội của game đó nên chọn lại từ đầu.
            var had = picked.length;
            picked = [];
            input.disabled = !gameId() || maxInvites() === 0;
            input.value = '';
            input.placeholder = !gameId() ? 'Chọn tựa game trước, rồi gõ tên người dùng hoặc tên trong game'
                : maxInvites() === 0 ? 'Tựa game này đội chỉ 1 người — không cần mời'
                : 'Gõ tên, tên trong game hoặc email đầy đủ (ít nhất 2 ký tự)';
            closeResults();
            renderChips();
            if (had) document.getElementById('memberHint').textContent = 'Đã đổi tựa game nên danh sách mời được làm lại. ' + document.getElementById('memberHint').textContent;
        });
        input.addEventListener('input', function () {
            clearTimeout(searchTimer);
            var q = input.value.trim();
            if (q.length < 2) { closeResults(); return; }
            searchTimer = setTimeout(function () { search(q); }, 300);
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); if (active >= 0) choose(active); return; }
            if (e.key === 'Escape') { closeResults(); return; }
            if (!results.length || (e.key !== 'ArrowDown' && e.key !== 'ArrowUp')) return;
            e.preventDefault();
            var step = e.key === 'ArrowDown' ? 1 : -1;
            for (var n = 0; n < results.length; n++) {
                active = (active + step + results.length) % results.length;
                if (!results[active].alreadyInTeam) break;
            }
            renderResults();
        });
        input.addEventListener('blur', function () { setTimeout(closeResults, 150); });
    }

    function wireSubmit() {
        var form = document.getElementById('create-form');
        var flash = document.getElementById('flash');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            Ui.clearAlert(flash);
            Ui.clearFieldErrors(form);

            // Cùng luật với CreateTeamRequest + ImageUploader phía máy chủ (giới hạn độ dài theo cột DB).
            var name = form.querySelector('#Name').value.trim();
            var tag = form.querySelector('#Tag').value.trim();
            var description = form.querySelector('#Description').value.trim();
            var logo = form.querySelector('#logoFile').files[0];
            var errors = {};
            if (name.length < 2 || name.length > 150) errors.Name = 'Tên đội phải từ 2 đến 150 ký tự.';
            if (!/^[A-Za-z0-9]{2,10}$/.test(tag)) errors.Tag = 'Thẻ đội gồm 2–10 chữ cái không dấu hoặc chữ số, không khoảng trắng.';
            if (!gameId()) errors.GameId = 'Vui lòng chọn tựa game chủ đạo.';
            if (logo && !/\.(jpe?g|png|webp)$/i.test(logo.name)) errors.logoFile = 'Logo phải có định dạng JPG, PNG hoặc WEBP.';
            else if (logo && logo.size > 2 * 1024 * 1024) errors.logoFile = 'Logo không được vượt quá 2MB.';
            if (description.length > 500) errors.Description = 'Giới thiệu đội tối đa 500 ký tự.';
            if (picked.length > maxInvites()) errors.InviteAccountIds = 'Chỉ mời được tối đa ' + maxInvites() + ' tuyển thủ cho tựa game này.';
            if (Object.keys(errors).length) {
                Ui.applyFieldErrors(form, errors);
                Ui.showAlert(flash, 'danger', 'Vui lòng sửa các ô được đánh dấu.');
                return;
            }

            var fd = new FormData();
            fd.append('Name', name);
            fd.append('Tag', tag);
            fd.append('GameId', String(gameId()));
            if (description) fd.append('Description', description);
            picked.forEach(function (p) { fd.append('InviteAccountIds', String(p.accountId)); });
            if (logo) fd.append('logoFile', logo);

            var btn = form.querySelector('button[type="submit"]');
            btn.disabled = true;
            try {
                var res = await Api.postForm('/api/teams', fd);
                Ui.setFlash(res.message, 'success');
                location.href = '/team/dashboard.html?id=' + res.data;   // trang quản trị: mã mời, roster
            } catch (err) {
                Ui.applyFieldErrors(form, err.fieldErrors);
                Ui.showAlert(flash, 'danger', err.message);
                btn.disabled = false;
            }
        });
    }

    document.addEventListener('DOMContentLoaded', async function () {
        var me = await Ui.requireAuth(['PLAYER']);
        if (!me) return;
        await populateGames();
        wireLogoPreview();
        wireDescription();
        wireMemberPicker();
        wireSubmit();
    });
})();
