// Ô ảnh bìa giải — dùng ở bước 1 trình tạo giải và trang Sửa thông tin.
// Chọn tệp từ máy HOẶC dán liên kết ảnh (kể cả link trang Google Ảnh); máy chủ tải ảnh về và lưu
// (POST /api/TournamentSetup/{id}/banner). Ảnh lưu ngay khi chọn, không chờ nút Lưu của form.
window.BannerPicker = (function () {
    function mount(root, opts) {
        // opts: { tournamentId: () => number|null, current: string|null, onSaved: function(url) }
        root.innerHTML =
            '<div class="banner-picker">' +
            '<div class="banner-preview" data-preview><span>Chưa có ảnh bìa</span></div>' +
            '<div class="banner-actions">' +
            '<label class="btn btn-ghost btn-sm mb-0">Chọn ảnh từ máy<input type="file" accept="image/jpeg,image/png,image/webp,image/gif" hidden data-file></label>' +
            '<div class="banner-link"><input class="input" type="url" maxlength="2048" placeholder="Hoặc dán liên kết ảnh (https://…)" data-url>' +
            '<button type="button" class="btn btn-ghost btn-sm" data-use-url>Dùng liên kết</button></div>' +
            '<span class="field-hint">JPG, PNG, WEBP hoặc GIF, tối đa 5 MB. Nên dùng ảnh ngang tỉ lệ 16:9.</span>' +
            '<div data-flash aria-live="polite"></div></div></div>';

        var preview = root.querySelector('[data-preview]');
        var flash = root.querySelector('[data-flash]');
        function show(url) {
            var safe = url && (url.charAt(0) === '/' || url.indexOf('blob:') === 0 ? url : Ui.safeUrl(url));
            preview.style.backgroundImage = safe ? 'url("' + safe.replace(/"/g, '%22') + '")' : '';
            preview.classList.toggle('has-image', !!safe);
        }
        show(opts.current);

        // Trình tạo giải: bước 1 chưa có mã giải -> giữ ảnh đã chọn, tải lên ngay sau khi lưu bước 1 (flush()).
        var pending = null;
        async function upload(fd, previewUrl) {
            var id = opts.tournamentId();
            Ui.clearAlert(flash);
            if (!id) {
                pending = fd;
                show(previewUrl);
                Ui.showAlert(flash, 'success', 'Đã chọn ảnh bìa — ảnh được lưu khi bạn bấm "Lưu & sang bước 2".');
                return;
            }
            root.classList.add('is-busy');
            try {
                var res = await Api.postForm('/api/TournamentSetup/' + id + '/banner', fd);
                show(res.bannerUrl);
                Ui.showAlert(flash, 'success', res.message || 'Đã cập nhật ảnh bìa.');
                if (opts.onSaved) opts.onSaved(res.bannerUrl);
            } catch (err) {
                Ui.showAlert(flash, 'danger', err.message || 'Không lưu được ảnh bìa.');
            } finally {
                root.classList.remove('is-busy');
            }
        }

        root.querySelector('[data-file]').addEventListener('change', function () {
            var f = this.files && this.files[0];
            if (!f) return;
            if (f.size > 5 * 1024 * 1024) { Ui.showAlert(flash, 'danger', 'Ảnh bìa tối đa 5 MB.'); this.value = ''; return; }
            var fd = new FormData(); fd.append('file', f);
            upload(fd, URL.createObjectURL(f));
            this.value = '';
        });
        root.querySelector('[data-use-url]').addEventListener('click', function () {
            var url = root.querySelector('[data-url]').value.trim();
            if (!/^https?:\/\//i.test(url)) { Ui.showAlert(flash, 'danger', 'Dán liên kết bắt đầu bằng http:// hoặc https://.'); return; }
            var fd = new FormData(); fd.append('url', url);
            upload(fd, null);
        });
        return {
            show: show,
            flush: async function () { if (pending && opts.tournamentId()) { var fd = pending; pending = null; await upload(fd); } }
        };
    }
    return { mount: mount };
})();
