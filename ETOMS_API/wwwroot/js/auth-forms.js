// Helper dùng chung cho các trang đăng ký / quên / đặt lại / xác minh / đổi mật khẩu.
(function () {
    const nativeFetch = window.fetch.bind(window);

    async function post(url, body) {
        let res;
        try {
            res = await nativeFetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
        } catch {
            return { ok: false, status: 0, data: { message: 'Không kết nối được máy chủ. Vui lòng thử lại.' } };
        }
        let data = null;
        try { data = await res.json(); } catch { /* body rỗng */ }
        if (res.status === 429) data = { message: 'Bạn thao tác quá nhanh. Vui lòng đợi một lát rồi thử lại.' };
        return { ok: res.ok, status: res.status, data };
    }

    function showMessage(el, text, kind) {
        el.textContent = text || '';
        el.className = 'form-msg ' + (kind || '');
        el.hidden = !text;
    }

    // Nút hiện spinner + khóa nút, đồng thời đặt các ô nhập về chỉ-đọc để tránh sửa/gửi trùng khi đang chờ.
    function setBusy(btn, busy, busyText) {
        if (!btn) return;
        if (busy) {
            btn.dataset.label = btn.textContent;
            btn.innerHTML = '<span class="reg-spinner" aria-hidden="true"></span>' + (busyText || 'Đang xử lý...');
            btn.setAttribute('aria-busy', 'true');
        } else {
            if (btn.dataset.label) btn.textContent = btn.dataset.label;
            btn.removeAttribute('aria-busy');
        }
        btn.disabled = busy;
        if (btn.form) btn.form.querySelectorAll('input').forEach(i => { i.readOnly = busy; });
    }

    // Thông báo dạng "đang tải" (spinner + chữ), dùng cho trang xác minh.
    function showLoading(el, text) {
        el.className = 'form-msg loading';
        el.innerHTML = '<span class="reg-spinner" aria-hidden="true"></span>';
        el.appendChild(document.createTextNode(text || 'Đang xử lý...'));
        el.hidden = false;
    }

    // Phải khớp PasswordPolicy.cs ở backend.
    function passwordError(pw) {
        if (!pw || pw.length < 8 || pw.length > 72) return 'Mật khẩu phải từ 8 đến 72 ký tự.';
        if (!/[A-Z]/.test(pw) || !/[a-z]/.test(pw) || !/[0-9]/.test(pw))
            return 'Mật khẩu phải gồm chữ hoa, chữ thường và chữ số.';
        return null;
    }

    function validEmail(v) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test((v || '').trim()); }

    function queryParam(name) { return new URLSearchParams(location.search).get(name); }

    window.AuthForms = { post, showMessage, showLoading, setBusy, passwordError, validEmail, queryParam };
})();