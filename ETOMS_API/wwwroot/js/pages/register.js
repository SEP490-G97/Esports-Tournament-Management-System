// wwwroot/js/pages/register.js
// AUTH-01: STUB TẠM thay module AUTH (Vũ Đức Hải) — trang này trước đây nạp nhầm script của
// dev-auth nên không đăng ký được gì cả. Không phải bài làm cá nhân của Hải.
(function () {
    var form = document.getElementById('register-form');
    var fullName = document.getElementById('reg-fullname');
    var dob = document.getElementById('reg-dob');
    var email = document.getElementById('reg-email');
    var password = document.getElementById('reg-password');
    var confirm = document.getElementById('reg-confirm');
    var agree = document.getElementById('reg-agree');
    var submitBtn = form.querySelector('.reg-submit');
    var errorBox = document.getElementById('reg-error');

    // Không cho chọn ngày sinh trong tương lai.
    dob.max = new Date().toISOString().slice(0, 10);

    function clearErrors() {
        errorBox.style.display = 'none';
        errorBox.textContent = '';
        form.querySelectorAll('[data-field-error]').forEach(function (el) { el.textContent = ''; });
    }

    function showGeneralError(msg) {
        errorBox.textContent = msg;
        errorBox.style.display = 'block';
    }

    function showFieldError(field, msg) {
        var el = form.querySelector('[data-field-error="' + field + '"]');
        if (el) el.textContent = msg; else showGeneralError(msg);
    }

    function setLoading(on) {
        submitBtn.disabled = on;
        submitBtn.textContent = on ? 'Đang đăng ký...' : 'Đăng ký tài khoản';
    }

    // Tuổi tính theo ngày sinh, không chỉ trừ năm (để đúng cả khi chưa tới sinh nhật năm nay).
    function calcAge(birthIso) {
        var b = new Date(birthIso + 'T00:00:00');
        var now = new Date();
        var age = now.getFullYear() - b.getFullYear();
        var hasHadBirthdayThisYear = (now.getMonth() > b.getMonth()) ||
            (now.getMonth() === b.getMonth() && now.getDate() >= b.getDate());
        if (!hasHadBirthdayThisYear) age--;
        return age;
    }

    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        clearErrors();

        var hasError = false;
        if (!fullName.value.trim()) { showFieldError('FullName', 'Vui lòng nhập họ tên.'); hasError = true; }
        if (!dob.value) {
            showFieldError('DateOfBirth', 'Vui lòng chọn ngày sinh.'); hasError = true;
        } else if (calcAge(dob.value) < 18) {
            showFieldError('DateOfBirth', 'Bạn phải đủ 18 tuổi trở lên mới được tạo tài khoản.'); hasError = true;
        }
        if (!email.value.trim()) { showFieldError('Email', 'Vui lòng nhập email.'); hasError = true; }
        if (password.value.length < 8) { showFieldError('Password', 'Mật khẩu phải có ít nhất 8 ký tự.'); hasError = true; }
        else if (password.value !== confirm.value) { showFieldError('Password', 'Mật khẩu xác nhận không khớp.'); hasError = true; }
        if (!agree.checked) { showGeneralError('Bạn cần đồng ý với Điều khoản sử dụng và Chính sách bảo mật.'); hasError = true; }
        if (hasError) return;

        setLoading(true);
        try {
            var res = await fetch((window.Auth ? Auth.API_BASE : '') + '/auth/register', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    fullName: fullName.value.trim(),
                    dateOfBirth: dob.value,
                    email: email.value.trim(),
                    password: password.value
                })
            });
            var data = await res.json().catch(function () { return {}; });
            if (res.ok) {
                Ui.setFlash(data.message || 'Đăng ký thành công. Vui lòng đăng nhập.', 'success');
                location.href = '/auth/login.html';
                return;
            }
            if (data.fieldErrors) {
                Object.keys(data.fieldErrors).forEach(function (f) {
                    var v = data.fieldErrors[f];
                    showFieldError(f, Array.isArray(v) ? v[0] : v);
                });
            } else {
                showGeneralError(data.message || 'Đăng ký thất bại. Vui lòng thử lại.');
            }
        } catch (err) {
            showGeneralError('Không kết nối được máy chủ. Vui lòng thử lại.');
        } finally {
            setLoading(false);
        }
    });
})();
