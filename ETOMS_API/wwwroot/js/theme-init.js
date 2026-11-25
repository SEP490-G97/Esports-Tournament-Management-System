// Áp dụng chế độ sáng/tối TRƯỚC khi CSS tải xong, tránh nháy sai màu (FOUC).
// Mặc định tối (bản sắc ETOMS) — chỉ đổi sang sáng nếu người dùng đã tự chọn trước đó.
// Nạp bằng <script> thường (không defer/async) trong <head>, TRƯỚC thẻ <link> site.css.
(function () {
    try {
        if (localStorage.getItem('etoms-theme') === 'light') {
            document.documentElement.setAttribute('data-theme', 'light');
        }
    } catch (e) {}
})();
