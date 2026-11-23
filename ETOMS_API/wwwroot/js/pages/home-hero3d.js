// Ô thống kê ở banner trang chủ nghiêng 3D theo chuột (CSS transform, không cần WebGL).
// Rồng ôm trang chủ nằm ở home-dragon.js.

// ===== Ô thống kê nghiêng theo chuột (không cần WebGL) =====
(function () {
    var grid = document.getElementById('hero-stats');
    if (!grid || !window.matchMedia || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    if (window.matchMedia('(hover: none)').matches) return;
    var tiles = grid.querySelectorAll('.stat-tile');
    grid.addEventListener('mousemove', function (e) {
        tiles.forEach(function (t) {
            var r = t.getBoundingClientRect();
            var dx = (e.clientX - (r.left + r.width / 2)) / r.width;
            var dy = (e.clientY - (r.top + r.height / 2)) / r.height;
            t.style.transform = 'perspective(700px) rotateX(' + (-dy * 10).toFixed(2) + 'deg) rotateY(' + (dx * 12).toFixed(2) + 'deg) translateZ(8px)';
        });
    });
    grid.addEventListener('mouseleave', function () {
        tiles.forEach(function (t) { t.style.transform = ''; });
    });
})();
