/* ADM-06 – Bảng thống kê toàn hệ thống. Biểu đồ vẽ bằng SVG/DOM thuần, mọi chữ đặt bằng textContent. */
(async function () {
    'use strict';

    const U = window.AdminUI;
    const el = U.el;
    const $ = (id) => document.getElementById(id);
    const SVG_NS = 'http://www.w3.org/2000/svg';

    await Auth.requireAuth(['ADMIN']);
    U.renderNav('dashboard');

    const STATUS_LABEL = {
        DRAFT: 'Bản nháp', PUBLISHED: 'Đã công bố', ONGOING: 'Đang diễn ra', COMPLETED: 'Đã kết thúc', CANCELLED: 'Đã hủy'
    };

    let seq = 0;
    let lastSeries = [];
    let showTable = false;
    let hasData = false; // đã từng hiển thị số liệu: lần tải sau chỉ làm mờ, không dựng lại khung xương

    const page = $('stats').closest('.ax-page');
    page.classList.add('ax-host');
    page.insertBefore(el('div', { class: 'ax-progress', attrs: { 'aria-hidden': 'true' } }), page.firstChild);

    // ------------------------------------------------------------ trạng thái đang tải
    function skelBar(w, h, mt) {
        return el('div', { class: 'ax-skel', style: 'width:' + w + ';height:' + h + 'px;' + (mt ? 'margin-top:' + mt + 'px' : '') });
    }

    function showSkeleton() {
        const stats = $('stats');
        U.clear(stats);
        for (let i = 0; i < 4; i++) {
            stats.appendChild(el('div', { class: 'ax-stat', attrs: { 'aria-hidden': 'true' } },
                skelBar('55%', 12), skelBar('40%', 30, 12), skelBar('80%', 10, 12)));
        }
        U.clear($('chart-users'));
        $('chart-users').appendChild(el('div', { class: 'ax-skel', style: 'width:100%;height:100%', attrs: { 'aria-hidden': 'true' } }));
        $('users-sub').textContent = '';
        ['bars-games', 'bars-status'].forEach((id) => {
            const box = $(id);
            U.clear(box);
            for (let i = 0; i < 4; i++) box.appendChild(el('div', { style: 'margin-bottom:14px', attrs: { 'aria-hidden': 'true' } }, skelBar(100 - i * 18 + '%', 14)));
        });
    }

    function setLoading(on) {
        page.classList.toggle('is-loading', on);
        page.setAttribute('aria-busy', on ? 'true' : 'false');
        $('refresh').disabled = on;
        $('range').disabled = on;
        $('refresh').textContent = on ? 'Đang tải…' : 'Làm mới';
        if (on && !hasData) showSkeleton();
    }

    function showError(message) {
        $('error').hidden = false;
        U.clear($('error'));
        $('error').appendChild(document.createTextNode(message + ' '));
        $('error').appendChild(el('button', { type: 'button', class: 'ax-btn ghost sm', text: 'Thử lại', onclick: load }));
        $('updated').textContent = 'Không tải được số liệu.';
        if (!hasData) { // bỏ khung xương, tránh để màn hình "treo" ở trạng thái đang tải
            U.clear($('stats')); U.clear($('chart-users')); U.clear($('bars-games')); U.clear($('bars-status'));
        }
    }

    // ------------------------------------------------------------ tải dữ liệu
    async function load() {
        const my = ++seq;
        const days = parseInt($('range').value, 10) || 30;
        const tzOffset = -new Date().getTimezoneOffset(); // phút so với UTC, VN = +420
        $('error').hidden = true;
        setLoading(true);

        const res = await U.api('/api/admin/stats/overview?days=' + days + '&tzOffset=' + tzOffset);
        if (my !== seq) return; // phản hồi cũ bị bỏ; yêu cầu mới hơn sẽ tắt trạng thái tải
        setLoading(false);

        if (!res.ok) {
            showError(U.errMsg(res, 'Không tải được số liệu thống kê.'));
            return;
        }
        hasData = true;
        render(res.data);
    }

    function render(d) {
        $('updated').textContent = 'Cập nhật lúc ' + U.fmtDateTime(d.generatedAt);

        // ---- thẻ chỉ số
        const stats = $('stats');
        U.clear(stats);
        const dis = d.disputes;
        stats.appendChild(stat('Tổng người dùng', U.fmtNumber(d.totalAccounts), '+' + U.fmtNumber(d.newUsersInRange) + ' trong ' + d.days + ' ngày qua'));
        stats.appendChild(stat('Giải đang diễn ra', U.fmtNumber(d.ongoingTournaments),
            U.fmtNumber((d.tournamentsByStatus.PUBLISHED || 0)) + ' giải đã công bố, chưa bắt đầu'));
        stats.appendChild(stat('Trận đã hoàn thành', U.fmtNumber(d.completedMatches), 'Đã xác nhận kết quả hoặc xử thắng kỹ thuật'));
        stats.appendChild(stat('Tỉ lệ trận tranh chấp',
            dis.ratePercent === null || dis.ratePercent === undefined ? '—' : String(dis.ratePercent).replace('.', ',') + '%',
            U.fmtNumber(dis.disputedMatches) + '/' + U.fmtNumber(dis.playedMatches) + ' trận có tranh chấp · ' + U.fmtNumber(dis.openDisputes) + ' đang mở'));

        // ---- người dùng mới
        lastSeries = d.newUsersDaily;
        const total = lastSeries.reduce((s, p) => s + p.count, 0);
        const peak = lastSeries.reduce((m, p) => (p.count > m.count ? p : m), lastSeries[0] || { count: 0, date: '' });
        $('users-sub').textContent = U.fmtNumber(total) + ' tài khoản mới trong ' + d.days + ' ngày' +
            (peak && peak.count > 0 ? ' · cao nhất ' + peak.count + ' (' + shortDate(peak.date) + ')' : '');
        $('chart-users').setAttribute('aria-label',
            'Biểu đồ đường người dùng đăng ký mới theo ngày, tổng ' + total + ' trong ' + d.days + ' ngày');
        drawLine();
        buildTable();

        // ---- top game
        drawBars($('bars-games'),
            d.topGames.map((g) => ({ label: g.title, sub: g.shortCode, value: g.count, unit: 'giải' })),
            'Chưa có giải đấu nào được công bố.');

        // ---- giải theo trạng thái
        const order = ['DRAFT', 'PUBLISHED', 'ONGOING', 'COMPLETED', 'CANCELLED'];
        drawBars($('bars-status'),
            order.map((k) => ({ label: STATUS_LABEL[k], value: d.tournamentsByStatus[k] || 0, unit: 'giải' })),
            'Chưa có giải đấu.');

        $('definitions').textContent =
            'Cách tính: “Trận đã hoàn thành” gồm trạng thái VERIFIED và WALKOVER. “Tỉ lệ tranh chấp” = số trận đã diễn ra ' +
            '(LIVE, WAITING_REPORT, DISPUTED, VERIFIED) từng có ít nhất một tranh chấp / tổng số trận đã diễn ra. ' +
            'Ngày trên biểu đồ tính theo múi giờ của trình duyệt.';
    }

    function stat(label, value, sub) {
        return el('div', { class: 'ax-stat' },
            el('div', { class: 'ax-stat-label', text: label }),
            el('div', { class: 'ax-stat-value', text: value }),
            el('div', { class: 'ax-stat-sub', text: sub }));
    }

    // ------------------------------------------------------------ biểu đồ đường
    function shortDate(s) { const p = s.split('-'); return p[2] + '/' + p[1]; }
    function longDate(s) { const p = s.split('-'); return p[2] + '/' + p[1] + '/' + p[0]; }

    function niceScale(maxValue) {
        if (maxValue <= 4) return { max: 4, step: 1 };
        const raw = maxValue / 4;
        const p = Math.pow(10, Math.floor(Math.log10(raw)));
        const m = raw / p;
        const nm = m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10;
        const step = Math.max(1, nm * p);
        return { max: step * 4, step: step };
    }

    function svgEl(name, attrs) {
        const n = document.createElementNS(SVG_NS, name);
        for (const k of Object.keys(attrs || {})) n.setAttribute(k, attrs[k]);
        return n;
    }

    function drawLine() {
        const box = $('chart-users');
        U.clear(box);
        const pts = lastSeries;
        if (!pts.length) return;

        const W = Math.max(box.clientWidth, 280);
        const H = Math.max(box.clientHeight, 200);
        const m = { l: 40, r: 14, t: 14, b: 28 };
        const n = pts.length;
        const scale = niceScale(Math.max.apply(null, pts.map((p) => p.count)));
        const x = (i) => m.l + (n === 1 ? 0 : (i * (W - m.l - m.r)) / (n - 1));
        const y = (v) => m.t + (1 - v / scale.max) * (H - m.t - m.b);

        const svg = svgEl('svg', { viewBox: '0 0 ' + W + ' ' + H, preserveAspectRatio: 'none', 'aria-hidden': 'true' });

        // lưới ngang + nhãn trục Y
        for (let v = 0; v <= scale.max + 0.0001; v += scale.step) {
            svg.appendChild(svgEl('line', { class: 'grid', x1: m.l, x2: W - m.r, y1: y(v), y2: y(v) }));
            const t = svgEl('text', { x: m.l - 8, y: y(v) + 4, 'text-anchor': 'end' });
            t.textContent = String(Math.round(v));
            svg.appendChild(t);
        }

        // nhãn trục X: tối đa ~6 nhãn, cách đều
        const labelCount = Math.min(6, n);
        const used = {};
        for (let k = 0; k < labelCount; k++) {
            const i = labelCount === 1 ? 0 : Math.round((k * (n - 1)) / (labelCount - 1));
            if (used[i]) continue;
            used[i] = true;
            const anchor = i === 0 ? 'start' : i === n - 1 ? 'end' : 'middle';
            const t = svgEl('text', { x: x(i), y: H - 8, 'text-anchor': anchor });
            t.textContent = shortDate(pts[i].date);
            svg.appendChild(t);
        }

        // vùng + đường
        const line = pts.map((p, i) => (i === 0 ? 'M' : 'L') + x(i).toFixed(1) + ' ' + y(p.count).toFixed(1)).join(' ');
        svg.appendChild(svgEl('path', { class: 'area', d: line + ' L' + x(n - 1).toFixed(1) + ' ' + y(0) + ' L' + x(0).toFixed(1) + ' ' + y(0) + ' Z' }));
        svg.appendChild(svgEl('path', { class: 'line', d: line }));

        // lớp hover: vạch dọc + chấm + tooltip
        const cross = svgEl('line', { class: 'cross', y1: m.t, y2: H - m.b, visibility: 'hidden' });
        const dot = svgEl('circle', { class: 'dot', r: 5, visibility: 'hidden' });
        svg.appendChild(cross);
        svg.appendChild(dot);
        const hit = svgEl('rect', { x: m.l, y: 0, width: W - m.l - m.r, height: H, fill: 'transparent' });
        svg.appendChild(hit);
        box.appendChild(svg);

        const tip = el('div', { class: 'ax-tip', hidden: true });
        box.appendChild(tip);

        function show(evt) {
            const rect = svg.getBoundingClientRect();
            const px = ((evt.clientX - rect.left) / rect.width) * W;
            let i = n === 1 ? 0 : Math.round(((px - m.l) / (W - m.l - m.r)) * (n - 1));
            i = Math.max(0, Math.min(n - 1, i));
            const p = pts[i];
            cross.setAttribute('x1', x(i)); cross.setAttribute('x2', x(i)); cross.setAttribute('visibility', 'visible');
            dot.setAttribute('cx', x(i)); dot.setAttribute('cy', y(p.count)); dot.setAttribute('visibility', 'visible');

            U.clear(tip);
            tip.appendChild(document.createTextNode(longDate(p.date) + ': '));
            tip.appendChild(el('b', { text: U.fmtNumber(p.count) }));
            tip.appendChild(document.createTextNode(' tài khoản mới'));
            tip.hidden = false;
            const left = Math.max(70, Math.min(rect.width - 70, (x(i) / W) * rect.width));
            tip.style.left = left + 'px';
            tip.style.top = (y(p.count) / H) * rect.height + 'px';
        }
        function hide() {
            cross.setAttribute('visibility', 'hidden');
            dot.setAttribute('visibility', 'hidden');
            tip.hidden = true;
        }
        hit.addEventListener('pointermove', show);
        hit.addEventListener('pointerdown', show);
        hit.addEventListener('pointerleave', hide);
    }

    function buildTable() {
        const wrap = $('table-users');
        U.clear(wrap);
        const rows = lastSeries.slice().reverse().map((p) =>
            el('tr', null, el('td', { text: longDate(p.date) }), el('td', { class: 'num', text: U.fmtNumber(p.count) })));
        wrap.appendChild(el('table', { class: 'ax-table' },
            el('thead', null, el('tr', null,
                el('th', { text: 'Ngày', attrs: { scope: 'col' } }),
                el('th', { class: 'num', text: 'Tài khoản mới', attrs: { scope: 'col' } }))),
            el('tbody', null, ...rows)));
    }

    // ------------------------------------------------------------ biểu đồ cột ngang
    function drawBars(container, rows, emptyText) {
        U.clear(container);
        const max = rows.reduce((m, r) => Math.max(m, r.value), 0);
        if (!rows.length || max === 0) {
            container.appendChild(el('div', { class: 'ax-empty', text: emptyText }));
            return;
        }
        const list = el('div', { class: 'ax-bars', attrs: { role: 'list' } });
        rows.forEach((r) => {
            const tip = r.label + ': ' + r.value + ' ' + (r.unit || '');
            list.appendChild(el('div', { class: 'ax-bar-row', title: tip, attrs: { role: 'listitem' } },
                el('div', { class: 'ax-bar-label' }, r.label, r.sub ? el('span', { class: 'ax-code ms-2', text: r.sub }) : null),
                el('div', { class: 'ax-bar-track' },
                    el('div', { class: 'ax-bar-fill', style: 'width:' + (r.value / max) * 100 + '%' })),
                el('div', { class: 'ax-bar-val', text: U.fmtNumber(r.value) })));
        });
        container.appendChild(list);
    }

    // ------------------------------------------------------------ sự kiện
    $('range').addEventListener('change', load);
    $('refresh').addEventListener('click', load);
    $('toggle-table').addEventListener('click', () => {
        showTable = !showTable;
        $('table-users').hidden = !showTable;
        $('chart-users').hidden = showTable;
        $('toggle-table').setAttribute('aria-pressed', String(showTable));
        $('toggle-table').textContent = showTable ? 'Xem dạng biểu đồ' : 'Xem dạng bảng';
        if (!showTable) drawLine();
    });

    let resizeTimer;
    if (window.ResizeObserver) {
        new ResizeObserver(() => {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(() => { if (!showTable && lastSeries.length) drawLine(); }, 120);
        }).observe($('chart-users'));
    }

    load();
})();