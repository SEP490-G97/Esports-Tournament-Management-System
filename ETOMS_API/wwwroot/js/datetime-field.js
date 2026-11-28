// Ô ngày giờ dùng chung (góp ý 28/11): thay ô datetime-local của trình duyệt (kiểu Mỹ mm/dd/yyyy, SA/CH) bằng
//   [lịch] dd/mm/yyyy | [đồng hồ] HH:mm ▾
// - Gõ trực tiếp được: ngày tự chèn "/" (28112026 -> 28/11/2026), giờ tự chèn ":" (1930 -> 19:30, 9 -> 09:00).
// - Bấm biểu tượng / ô để mở bảng chọn tự vẽ: lịch tiếng Việt (tuần bắt đầu Thứ 2) và bảng giờ gọn (24 giờ + phút 5').
// Ô datetime-local gốc VẪN GIỮ (ẩn) làm nơi lưu "yyyy-MM-ddTHH:mm": code kiểm mốc thời gian (Ui.liveTimeline) và code
// lưu đọc thẳng ô gốc như cũ; gán .value bằng JS vào ô gốc thì giao diện tự cập nhật.
// Dùng: <input type="datetime-local" id="x" data-dt-split> (tự dựng khi nạp script) hoặc DateTimeField.enhance(el).
window.DateTimeField = (function () {
    var CAL = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="4" width="18" height="18" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/></svg>';
    var CLOCK = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>';
    var CARET = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg>';
    var WEEK = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
    var DEFAULT_TIME = '19:00';   // chọn ngày mà chưa có giờ: gợi ý giờ thi đấu phổ biến, người dùng chỉnh nếu khác
    var valueDesc = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
    var pad = function (n) { return String(n).padStart(2, '0'); };
    var openBox = null;            // chỉ mở một bảng chọn trên toàn trang

    // ---- chuyển đổi ----
    function isoDate(y, m, d) { return y + '-' + pad(m) + '-' + pad(d); }                 // yyyy-MM-dd
    function viDate(iso) { return iso ? iso.slice(8, 10) + '/' + iso.slice(5, 7) + '/' + iso.slice(0, 4) : ''; }
    // "28/11/2026", "28/11/26", "2/1/2026" -> yyyy-MM-dd; sai -> null
    function parseViDate(text) {
        var m = /^(\d{1,2})\/(\d{1,2})\/(\d{2}|\d{4})$/.exec((text || '').trim());
        if (!m) return null;
        var d = +m[1], mo = +m[2], y = m[3].length === 2 ? 2000 + +m[3] : +m[3];
        var dt = new Date(y, mo - 1, d);
        return dt.getFullYear() === y && dt.getMonth() === mo - 1 && dt.getDate() === d ? isoDate(y, mo, d) : null;
    }
    // "19:30", "1930", "930", "9", "19" -> HH:mm; sai -> null
    function parseTime(text) {
        var t = (text || '').trim(), h, mi;
        var m = /^(\d{1,2}):(\d{1,2})$/.exec(t);
        if (m) { h = +m[1]; mi = +m[2]; }
        else if (/^\d{1,4}$/.test(t)) {
            if (t.length <= 2) { h = +t; mi = 0; }
            else { h = +t.slice(0, t.length - 2); mi = +t.slice(-2); }
        } else return null;
        return h < 24 && mi < 60 ? pad(h) + ':' + pad(mi) : null;
    }
    // Gõ tới đâu chèn "/" tới đó: 28112026 -> 28/11/2026
    function maskDate(text) {
        var d = text.replace(/\D/g, '').slice(0, 8);
        return d.length > 4 ? d.slice(0, 2) + '/' + d.slice(2, 4) + '/' + d.slice(4) : d.length > 2 ? d.slice(0, 2) + '/' + d.slice(2) : d;
    }
    // Giờ: tính lại từ các chữ số mỗi lần gõ — 3 chữ số = H:MM (930 -> 9:30), 4 chữ số = HH:MM (1930 -> 19:30).
    // Người dùng tự gõ ":" (vd 9:5) thì giữ nguyên cách họ gõ. Khi rời ô sẽ chuẩn thành HH:mm (09:30, 09:05).
    function maskTime(text, userColon) {
        if (userColon) return text.replace(/[^\d:]/g, '').replace(/:(?=.*:)/g, '').slice(0, 5);
        var d = text.replace(/\D/g, '').slice(0, 4);
        return d.length === 3 ? d[0] + ':' + d.slice(1) : d.length === 4 ? d.slice(0, 2) + ':' + d.slice(2) : d;
    }

    function closeOpen() { if (openBox) { openBox.close(); openBox = null; } }
    document.addEventListener('mousedown', function (e) { if (openBox && !openBox.box.contains(e.target)) closeOpen(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeOpen(); });

    function enhance(input) {
        if (!input || input.dataset.dtfReady) return;
        input.dataset.dtfReady = '1';
        var fieldLabel = input.closest('.field') && input.closest('.field').querySelector('label');
        var label = input.getAttribute('aria-label') || (fieldLabel ? fieldLabel.textContent.replace('*', '').trim() : 'Ngày giờ');

        var box = document.createElement('div');
        box.className = 'dtf';
        box.innerHTML =
            '<div class="dtf-part dtf-date-part">' +
            '<button type="button" class="dtf-icon" data-open="date" tabindex="-1" aria-label="Mở lịch chọn ngày ' + label + '">' + CAL + '</button>' +
            '<input type="text" class="dtf-text dtf-date-input" inputmode="numeric" maxlength="10" placeholder="dd/mm/yyyy" autocomplete="off" aria-label="Ngày ' + label + ' (dd/mm/yyyy)" />' +
            '</div><span class="dtf-sep" aria-hidden="true"></span>' +
            '<div class="dtf-part dtf-time-part">' +
            '<button type="button" class="dtf-icon" data-open="time" tabindex="-1" aria-label="Mở bảng chọn giờ ' + label + '">' + CLOCK + '</button>' +
            '<input type="text" class="dtf-text dtf-time-input" inputmode="numeric" maxlength="5" placeholder="--:--" autocomplete="off" aria-label="Giờ ' + label + ' (HH:mm)" />' +
            '<button type="button" class="dtf-caret" data-open="time" tabindex="-1" aria-label="Mở bảng chọn giờ">' + CARET + '</button>' +
            '</div><div class="dtf-pop" hidden></div>';
        input.insertAdjacentElement('beforebegin', box);
        input.classList.add('dtf-source');
        // Bấm trong bảng chọn không làm ô đang gõ mất focus (tránh đọc lại chữ gõ dở giữa chừng).
        box.querySelector('.dtf-pop').addEventListener('mousedown', function (e) { e.preventDefault(); });

        var dateIn = box.querySelector('.dtf-date-input'), timeIn = box.querySelector('.dtf-time-input');
        var pop = box.querySelector('.dtf-pop');
        var state = { date: null, time: null, view: null, mode: null };   // view = tháng đang xem (Date ngày 1)

        function original() { return valueDesc.get.call(input) || ''; }
        function minIso() { input.dispatchEvent(new Event('focus')); return input.min || ''; }   // Ui.liveTimeline cập nhật min khi ô được chạm

        // Ô gốc -> giao diện
        function render() {
            var v = original();
            state.date = v ? v.slice(0, 10) : null;
            state.time = v ? v.slice(11, 16) : null;
            if (document.activeElement !== dateIn) dateIn.value = viDate(state.date);
            if (document.activeElement !== timeIn) timeIn.value = state.time || '';
        }
        // Giao diện -> ô gốc: đủ ngày và giờ hợp lệ mới ghi; báo sự kiện để phần kiểm mốc thời gian chạy như khi gõ.
        function commit() {
            var v = state.date && state.time ? state.date + 'T' + state.time : '';
            if (v !== original()) {
                valueDesc.set.call(input, v);
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
            dateIn.value = viDate(state.date); timeIn.value = state.time || '';
            box.classList.toggle('has-bad-text', false);
        }
        function readTyped() {
            var dTxt = dateIn.value.trim(), tTxt = timeIn.value.trim();
            var d = dTxt ? parseViDate(dTxt) : null, t = tTxt ? parseTime(tTxt) : null;
            var bad = (dTxt && !d) || (tTxt && !t);
            state.date = d; state.time = t || (d && !tTxt ? DEFAULT_TIME : null);
            if (bad) {   // gõ sai định dạng: giữ chữ để sửa, ô gốc để trống => báo "Chọn ngày giờ."
                if (original()) { valueDesc.set.call(input, ''); input.dispatchEvent(new Event('change', { bubbles: true })); }
                box.classList.add('has-bad-text');
                return;
            }
            commit();
        }

        // Gán .value bằng JS (nạp bản nháp, quay lại bước) -> cập nhật giao diện.
        Object.defineProperty(input, 'value', {
            configurable: true,
            get: function () { return valueDesc.get.call(this); },
            set: function (v) { valueDesc.set.call(this, v); render(); }
        });

        dateIn.addEventListener('input', function () { var p = dateIn.selectionStart === dateIn.value.length; dateIn.value = maskDate(dateIn.value); if (p) dateIn.setSelectionRange(dateIn.value.length, dateIn.value.length); });
        var userColon = false;
        timeIn.addEventListener('input', function (e) {
            if (e.data === ':') userColon = true;
            else if (timeIn.value.indexOf(':') < 0) userColon = false;   // gõ mới (chỉ có chữ số) -> tự chèn ":"
            timeIn.value = maskTime(timeIn.value, userColon);
        });
        // Sửa giờ đang có (vd xóa bớt "19:00") thì giữ dấu ":" như đang hiện, không tính lại.
        timeIn.addEventListener('focus', function () { userColon = timeIn.value.indexOf(':') >= 0; });
        [dateIn, timeIn].forEach(function (el) {
            // Bấm vào ô đang có giá trị thì bôi đen hết: gõ là thay luôn (vd ô giờ đang 19:00, gõ 2130).
            el.addEventListener('focus', function () { minIso(); setTimeout(function () { if (document.activeElement === el) el.select(); }, 0); });
            el.addEventListener('blur', readTyped);
            el.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') { e.preventDefault(); readTyped(); closeOpen(); }
                if (e.key === 'ArrowDown') { e.preventDefault(); open(el === dateIn ? 'date' : 'time'); }
            });
        });
        box.querySelectorAll('[data-open]').forEach(function (b) {
            b.addEventListener('click', function () {
                var mode = b.dataset.open;
                if (openBox && openBox.box === box && state.mode === mode) { closeOpen(); return; }
                open(mode);
            });
        });

        // ---- bảng chọn ----
        function open(mode) {
            closeOpen();
            readTypedSilently();
            state.mode = mode;
            if (mode === 'date') {
                var base = state.date ? new Date(+state.date.slice(0, 4), +state.date.slice(5, 7) - 1, 1) : new Date();
                state.view = new Date(base.getFullYear(), base.getMonth(), 1);
                drawCalendar();
            } else drawTime();
            pop.className = 'dtf-pop dtf-pop-' + mode;
            pop.hidden = false;
            box.classList.add('is-open');
            openBox = { box: box, close: function () { pop.hidden = true; pop.innerHTML = ''; state.mode = null; box.classList.remove('is-open'); } };
        }
        // Mở bảng khi đang gõ dở: lấy phần đã gõ đúng làm điểm bắt đầu, không báo lỗi.
        function readTypedSilently() {
            var d = parseViDate(dateIn.value), t = parseTime(timeIn.value);
            if (d) state.date = d;
            if (t) state.time = t;
        }

        function drawCalendar() {
            var min = minIso().slice(0, 10), today = new Date(), todayIso = isoDate(today.getFullYear(), today.getMonth() + 1, today.getDate());
            var y = state.view.getFullYear(), m = state.view.getMonth();
            var startOffset = (new Date(y, m, 1).getDay() + 6) % 7;            // Thứ 2 đầu tuần
            var daysIn = new Date(y, m + 1, 0).getDate();
            var cells = '';
            for (var i = 0; i < startOffset; i++) cells += '<span class="dtf-day is-blank"></span>';
            for (var d = 1; d <= daysIn; d++) {
                var iso = isoDate(y, m + 1, d), off = min && iso < min;
                cells += '<button type="button" class="dtf-day' + (iso === state.date ? ' is-selected' : '') + (iso === todayIso ? ' is-today' : '') +
                    '" data-day="' + iso + '"' + (off ? ' disabled' : '') + '>' + d + '</button>';
            }
            var prevOff = min && isoDate(y, m + 1, 1) <= min;
            pop.innerHTML =
                '<div class="dtf-cal-head"><button type="button" class="dtf-nav" data-nav="-1" aria-label="Tháng trước"' + (prevOff ? ' disabled' : '') + '>‹</button>' +
                '<span class="dtf-cal-title">Tháng ' + (m + 1) + ', ' + y + '</span>' +
                '<button type="button" class="dtf-nav" data-nav="1" aria-label="Tháng sau">›</button></div>' +
                '<div class="dtf-week">' + WEEK.map(function (w) { return '<span>' + w + '</span>'; }).join('') + '</div>' +
                '<div class="dtf-days">' + cells + '</div>' +
                '<div class="dtf-pop-foot"><button type="button" class="dtf-link" data-clear>Xóa</button>' +
                '<button type="button" class="dtf-link" data-today' + (min && todayIso < min ? ' disabled' : '') + '>Hôm nay</button></div>';
            pop.querySelectorAll('[data-nav]').forEach(function (b) {
                b.addEventListener('click', function () { state.view = new Date(y, m + (+b.dataset.nav), 1); drawCalendar(); });
            });
            pop.querySelectorAll('[data-day]').forEach(function (b) {
                b.addEventListener('click', function () { pickDate(b.dataset.day); });
            });
            pop.querySelector('[data-today]').addEventListener('click', function () { pickDate(todayIso); });
            pop.querySelector('[data-clear]').addEventListener('click', function () { state.date = null; state.time = null; commit(); closeOpen(); dateIn.focus(); });
        }
        function pickDate(iso) {
            state.date = iso;
            if (!state.time) state.time = DEFAULT_TIME;
            commit(); closeOpen();
            timeIn.focus(); timeIn.select();
        }

        function drawTime() {
            var cur = state.time || '', h = cur ? cur.slice(0, 2) : null, mi = cur ? cur.slice(3, 5) : null;
            var hours = '', mins = '';
            for (var i = 0; i < 24; i++) hours += '<button type="button" class="dtf-slot' + (pad(i) === h ? ' is-selected' : '') + '" data-h="' + pad(i) + '">' + pad(i) + '</button>';
            for (var j = 0; j < 60; j += 5) mins += '<button type="button" class="dtf-slot' + (pad(j) === mi ? ' is-selected' : '') + '" data-m="' + pad(j) + '">' + pad(j) + '</button>';
            pop.innerHTML =
                '<div class="dtf-time-cols"><div><div class="dtf-time-label">Giờ</div><div class="dtf-hours">' + hours + '</div></div>' +
                '<div><div class="dtf-time-label">Phút</div><div class="dtf-mins">' + mins + '</div></div></div>' +
                '<div class="dtf-pop-foot"><span class="dtf-hint">Có thể gõ thẳng, vd 1930</span><button type="button" class="dtf-link" data-done>Xong</button></div>';
            pop.querySelectorAll('[data-h]').forEach(function (b) {
                b.addEventListener('click', function () { state.time = b.dataset.h + ':' + (mi || '00'); h = b.dataset.h; mi = mi || '00'; applyTime(false); });
            });
            pop.querySelectorAll('[data-m]').forEach(function (b) {
                b.addEventListener('click', function () { state.time = (h || '19') + ':' + b.dataset.m; applyTime(true); });
            });
            pop.querySelector('[data-done]').addEventListener('click', closeOpen);
        }
        function applyTime(done) {
            if (state.date) commit(); else timeIn.value = state.time;   // chưa có ngày: giữ giờ đã chọn, chờ chọn ngày
            if (done) closeOpen(); else drawTime();
        }

        // Lỗi/khóa của ô gốc hiện lên khung mới.
        new MutationObserver(function () {
            box.classList.toggle('is-invalid', input.classList.contains('is-invalid'));
            var off = input.disabled || !!input.closest('fieldset:disabled');
            box.querySelectorAll('input, button').forEach(function (el) { el.disabled = off; });
            box.classList.toggle('is-disabled', off);
        }).observe(input, { attributes: true, attributeFilter: ['class', 'disabled'] });
        render();
    }

    function enhanceAll(root) {
        (root || document).querySelectorAll('input[type="datetime-local"][data-dt-split]').forEach(enhance);
    }

    // Script đặt cuối trang nên các ô đã có sẵn: dựng ngay, trước khi script của trang nạp giá trị.
    enhanceAll();
    document.addEventListener('DOMContentLoaded', function () { enhanceAll(); });

    return { enhance: enhance, enhanceAll: enhanceAll, parseViDate: parseViDate, parseTime: parseTime };
})();
