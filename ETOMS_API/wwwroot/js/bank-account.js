// Tài khoản nhận lệ phí của giải — dùng chung cho bước 4 trình tạo giải và trang Sửa thông tin.
// Server kiểm lại đúng các luật này (Utils/BankAccount.cs); ở đây chỉ để báo lỗi sớm, đúng ô.
// Chuỗi lưu: "<Ngân hàng> | STK: <số> | ChuTK: <TÊN KHÔNG DẤU>".
window.BankAccount = (function () {
    var banksPromise = null;

    function loadBanks() {
        if (!banksPromise) banksPromise = Api.get('/api/TournamentSetup/banks').then(function (r) {
            return Array.isArray(r) ? r : (r && r.data) || [];
        }).catch(function (e) { banksPromise = null; throw e; });
        return banksPromise;
    }

    // Đổ danh sách vào <select>; giữ giá trị đang chọn nếu có.
    async function fillSelect(select, selected) {
        try {
            var banks = await loadBanks();
            select.innerHTML = '<option value="">-- Chọn ngân hàng --</option>' + banks.map(function (b) {
                return '<option value="' + Ui.escapeHtml(b.code) + '">' + Ui.escapeHtml(b.code + ' — ' + b.name) + '</option>';
            }).join('');
            if (selected) {
                var match = banks.find(function (b) { return b.code.toLowerCase() === String(selected).toLowerCase(); });
                if (match) select.value = match.code;
            }
        } catch (e) {
            select.innerHTML = '<option value="">Không tải được danh sách ngân hàng</option>';
        }
    }

    // In hoa, bỏ dấu, gộp khoảng trắng — như tên chủ tài khoản trên app ngân hàng.
    function normalizeHolder(raw) {
        return String(raw || '').trim().replace(/đ/g, 'd').replace(/Đ/g, 'D')
            .normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/\s+/g, ' ').toUpperCase();
    }

    function onlyDigits(raw) { return String(raw || '').replace(/\D/g, ''); }

    // Trả về { field, message } nếu sai, null nếu hợp lệ. field khớp data-field-error của form.
    function validate(fee, bank, number, holder) {
        var b = (bank || '').trim(), n = onlyDigits(number), h = normalizeHolder(holder);
        if (!b && !n && !h) return fee > 0 ? { field: 'BankName', message: 'Giải có lệ phí phải nhập đủ ngân hàng, số tài khoản và chủ tài khoản nhận phí.' } : null;
        if (!b) return { field: 'BankName', message: 'Vui lòng chọn ngân hàng.' };
        if (!/^\d{6,20}$/.test(n)) return { field: 'AccountNumber', message: 'Số tài khoản chỉ gồm chữ số, dài 6–20 số.' };
        if (h.length < 2 || h.length > 100 || !/^[A-Z]+( [A-Z]+)*$/.test(h))
            return { field: 'AccountName', message: 'Tên chủ tài khoản chỉ gồm chữ cái và khoảng trắng, ví dụ NGUYEN VAN A.' };
        return null;
    }

    function parse(stored) {
        var m = /^(.+?) \| STK: (\d+) \| ChuTK: (.+)$/.exec(String(stored || '').trim());
        return m ? { bank: m[1], number: m[2], holder: m[3] } : null;
    }

    function compose(bank, number, holder) {
        var b = (bank || '').trim(), n = onlyDigits(number), h = normalizeHolder(holder);
        return (b || n || h) ? b + ' | STK: ' + n + ' | ChuTK: ' + h : '';
    }

    // Gắn hành vi cho ô: số tài khoản chỉ nhận chữ số, tên chủ tài khoản tự in hoa không dấu khi rời ô.
    function wireInputs(numberInput, holderInput) {
        numberInput.addEventListener('input', function () {
            var d = onlyDigits(numberInput.value);
            if (d !== numberInput.value) numberInput.value = d;
        });
        holderInput.addEventListener('blur', function () { holderInput.value = normalizeHolder(holderInput.value); });
    }

    return { fillSelect: fillSelect, normalizeHolder: normalizeHolder, validate: validate, parse: parse, compose: compose, wireInputs: wireInputs };
})();
