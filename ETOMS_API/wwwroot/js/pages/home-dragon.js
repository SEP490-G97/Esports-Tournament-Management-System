// Rồng vàng "Golden Dragon Anchor" trang chủ — dùng đúng tranh người dùng gửi 23/11 (images/dragon-frame.jpg,
// gốc "Golden Dragon Esports UI Frame.png" 1672×941). Bố cục theo tranh:
//  - Đầu rồng + thân ngang nằm trong dải .home-crown phía trên thanh menu; 2 móng trên ghim vào thanh menu.
//  - Thân hai bên ôm hai cạnh (chỉ trong lề trống, không đè nội dung); 2 móng dưới ghim vào mép trên Footer.
//  - 4 điểm ghim: vết nứt sáng, tia vàng, bụi năng lượng; trung tâm trang để trống.
// Chuyển động: mở màn tranh hiện ra từ sương, móng "đập" xuống mép (lóe sáng, tia lửa, rung), mắt rực lên;
// khi đậu: mắt sáng theo nhịp, vệt ánh kim lướt trên thân, vết nứt phập phồng, sương trôi, sét ở góc mây, tàn lửa bay.
// Phần nằm đè thanh menu/Footer được "tách nền" (nền tối → trong suốt) để chỉ còn móng, vết nứt và ánh sáng.
(function () {
    'use strict';
    var MIN_W = 1024, SRC = '/images/dragon-frame.jpg?v=20261123';
    var IW = 1672, IH = 941, BAR_T = 182, BAR_OVER = 76, BBAR_T = 782;
    var BG = [4, 8, 18];                                       // màu nền tối của tranh (trừ đi khi tách nền)
    var EYES = [[833, 91], [887, 99]];
    var CLAW_T = [[288, 204], [1380, 204]], CLAW_B = [[266, 802], [1402, 802]];
    var SIDE_U = [256, 600], SIDE_B = [560, 941];             // hàng ảnh của thân hai bên (trên, dưới)
    var SIDE_W = 360;                                          // bề rộng mỗi cạnh lấy từ tranh

    var crown = document.querySelector('.home-crown');
    if (!crown || !document.createElement('canvas').getContext) return;
    var reduce = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

    var cv = document.createElement('canvas');
    cv.className = 'home-dragon-canvas';
    cv.setAttribute('aria-hidden', 'true');
    cv.style.cssText = 'position:fixed;left:0;top:0;width:100vw;height:100vh;pointer-events:none;z-index:60;';
    document.body.appendChild(cv);
    var ctx = cv.getContext('2d');

    var img = new Image(), alphaC = null, sideU = null, sideB = null, glintC = null;
    var dpr = 1, VW = 0, VH = 0, L = null;
    var t0 = -1, botT0 = -1, raf = 0, running = false, lastNow = 0;
    var bolts = [], embers = [], sparks = [], nextBolt = 2.2, impactsT = false, impactsB = false, shakeT = 0, shakeB = 0;

    function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
    function easeOut(t) { t = clamp(t, 0, 1); return 1 - Math.pow(1 - t, 3); }
    function docRect(el) {
        var r = el.getBoundingClientRect();
        return { left: r.left + window.scrollX, top: r.top + window.scrollY, right: r.right + window.scrollX, bottom: r.bottom + window.scrollY, width: r.width, height: r.height };
    }
    function hash(i) { var x = Math.sin(i * 127.1) * 43758.5453; return x - Math.floor(x); }

    // ===== Chuẩn bị ảnh (một lần) =====
    // Tách nền: phần tối của tranh thành trong suốt, giữ màu/độ sáng (ngược của phép cộng sáng trên nền tối).
    function buildLayers() {
        alphaC = document.createElement('canvas'); alphaC.width = IW; alphaC.height = IH;
        var g = alphaC.getContext('2d', { willReadFrequently: true });
        g.drawImage(img, 0, 0);
        var d = g.getImageData(0, 0, IW, IH), px = d.data;
        for (var i = 0; i < px.length; i += 4) {
            var r = Math.max(0, px[i] - BG[0]), gg = Math.max(0, px[i + 1] - BG[1]), b = Math.max(0, px[i + 2] - BG[2]);
            var m = Math.max(r, gg, b) / 255;
            if (m < 0.075) { px[i + 3] = 0; continue; }       // bỏ nền và thân thanh ngang màu xanh đậm
            px[i] = Math.min(255, r / m); px[i + 1] = Math.min(255, gg / m); px[i + 2] = Math.min(255, b / m);
            px[i + 3] = Math.min(255, (m - 0.04) * 1.12 * 255);
        }
        g.putImageData(d, 0, 0);
        fogBig = makeNoiseFog(768, 256, 96, 5, 11, 0.42, 0.78, 0.85);
        fogFine = makeNoiseFog(512, 192, 64, 4, 37, 0.5, 0.86, 0.7);
        fbuf = document.createElement('canvas'); fbuf.width = 1024; fbuf.height = 256; fctx = fbuf.getContext('2d');
        glintC = document.createElement('canvas'); glintC.width = IW; glintC.height = BAR_T + BAR_OVER;
    }
    // Thân hai bên: cắt từ lớp đã tách nền; mờ dần ở đầu/cuối và mờ dần theo chiều ngang trước mép nội dung
    // (edges: [[hàng từ, hàng tới, x bắt đầu mờ, x hết hẳn]] tính theo pixel ảnh, phía trái; phía phải đối xứng).
    function buildSides(gutImg) {
        var g0 = Math.max(40, gutImg);
        sideU = cutSides(SIDE_U[0], SIDE_U[1], 0, 140, [[0, SIDE_U[1] - SIDE_U[0], g0 - 70, g0]]);
        var split = 770 - SIDE_B[0];
        sideB = cutSides(SIDE_B[0], SIDE_B[1], 110, 40, [[0, split, g0 - 70, g0], [split, SIDE_B[1] - SIDE_B[0], SIDE_W - 90, SIDE_W]]);
    }
    function cutSides(y0, y1, fadeTop, fadeBottom, edges) {
        var h = y1 - y0, c = document.createElement('canvas'); c.width = IW; c.height = h;
        var g = c.getContext('2d');
        g.drawImage(alphaC, 0, y0, SIDE_W, h, 0, 0, SIDE_W, h);
        g.drawImage(alphaC, IW - SIDE_W, y0, SIDE_W, h, IW - SIDE_W, 0, SIDE_W, h);
        g.globalCompositeOperation = 'destination-out';
        (edges || []).forEach(function (ed) {
            var a0 = Math.max(0, ed[2]), a1 = Math.max(a0 + 1, ed[3]), hh = ed[1] - ed[0];
            var l = g.createLinearGradient(a0, 0, a1, 0); l.addColorStop(0, 'rgba(0,0,0,0)'); l.addColorStop(1, 'rgba(0,0,0,1)');
            g.fillStyle = l; g.fillRect(a0, ed[0], IW / 2 - a0, hh);
            var r = g.createLinearGradient(IW - a0, 0, IW - a1, 0); r.addColorStop(0, 'rgba(0,0,0,0)'); r.addColorStop(1, 'rgba(0,0,0,1)');
            g.fillStyle = r; g.fillRect(IW / 2, ed[0], IW / 2 - a0, hh);
        });
        if (fadeTop) { var a = g.createLinearGradient(0, 0, 0, fadeTop); a.addColorStop(0, 'rgba(0,0,0,1)'); a.addColorStop(1, 'rgba(0,0,0,0)'); g.fillStyle = a; g.fillRect(0, 0, IW, fadeTop); }
        if (fadeBottom) { var b = g.createLinearGradient(0, h - fadeBottom, 0, h); b.addColorStop(0, 'rgba(0,0,0,0)'); b.addColorStop(1, 'rgba(0,0,0,1)'); g.fillStyle = b; g.fillRect(0, h - fadeBottom, IW, fadeBottom); }
        return c;
    }
    // Vân sương thật: nhiễu fractal (fbm) lặp liền theo chiều ngang, tự đổ bóng (mép trên cuộn sáng, đáy tối)
    // → ra khối mây khói cuộn có chiều sâu thay vì đốm tròn.
    function makeNoiseFog(w, h, cell, octaves, seed, lo, hi, maxA) {
        var c = document.createElement('canvas'); c.width = w; c.height = h;
        var g = c.getContext('2d'), d = g.createImageData(w, h), px = d.data;
        function vnoise(x, y, cs, gw, sd) {
            var gx = x / cs, gy = y / cs, x0 = Math.floor(gx), y0 = Math.floor(gy), fx = gx - x0, fy = gy - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            function v(ix, iy) { return hash(((ix % gw) + gw) % gw * 157 + iy * 311 + sd * 1013); }
            var a = v(x0, y0), b = v(x0 + 1, y0), cc = v(x0, y0 + 1), dd = v(x0 + 1, y0 + 1);
            return a + (b - a) * fx + (cc - a) * fy + (a - b - cc + dd) * fx * fy;
        }
        var dens = new Float32Array(w * h);
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) {
            var n = 0, amp = 0.5, cs = cell, tot = 0;
            for (var o = 0; o < octaves; o++) { n += amp * vnoise(x, y, cs, Math.round(w / cs), seed + o * 7); tot += amp; amp *= 0.5; cs /= 2; }
            var vy = y / h, edge = Math.sin(Math.PI * vy);                       // mỏng dần ở mép trên/dưới tấm
            dens[y * w + x] = (n / tot) * (0.55 + 0.45 * edge);
        }
        for (y = 0; y < h; y++) for (x = 0; x < w; x++) {
            var i = y * w + x, nv = dens[i], t = clamp((nv - lo) / (hi - lo), 0, 1);
            var a = t * t * (3 - 2 * t) * maxA;
            var up = y > 3 ? dens[i - 3 * w] : nv, lit = clamp((nv - up) * 9, -1, 1); // mật độ tăng dần xuống → mép trên được rọi sáng
            var k = i * 4;
            px[k] = 120 + 70 * Math.max(0, lit) + 18 * t; px[k + 1] = 126 + 58 * Math.max(0, lit) + 16 * t; px[k + 2] = 150 + 40 * Math.max(0, lit) + 10 * t;
            if (lit < 0) { px[k] *= 1 + lit * 0.35; px[k + 1] *= 1 + lit * 0.35; px[k + 2] *= 1 + lit * 0.3; }
            px[k + 3] = a * 255;
        }
        g.putImageData(d, 0, 0);
        return c;
    }
    var fogBig = null, fogFine = null, fbuf = null, fctx = null;
    // Một dải sương 2 lớp trôi ngược chiều (thị sai), vẽ ở nửa độ phân giải rồi phóng lên cho mềm;
    // mờ dần mép trên/dưới, "khoét" các vùng nội dung (erase: [x0, x1, mép mềm, độ khoét 0..1]).
    function fogBand(y, h, time, o) {
        if (h <= 2 || (o.a1 <= 0 && o.a2 <= 0)) return;
        var q = 0.5, bw = Math.ceil(L.W * q), bh = Math.ceil(h * q);
        if (fbuf.width < bw || fbuf.height < bh) { fbuf.width = Math.max(fbuf.width, bw); fbuf.height = Math.max(fbuf.height, bh); }
        var g = fctx;
        g.setTransform(1, 0, 0, 1, 0, 0); g.globalCompositeOperation = 'source-over'; g.globalAlpha = 1; g.clearRect(0, 0, fbuf.width, fbuf.height);   // xóa cả bộ đệm: phần sót lại sẽ lem thành vạch ở mép dải
        [[fogBig, o.a1, o.sp1, 1], [fogFine, o.a2, o.sp2, 0.7]].forEach(function (ly) {
            if (ly[1] <= 0) return;
            var th = bh * ly[3], tw = ly[0].width * th / ly[0].height, off = ((time * ly[2] * q) % tw + tw) % tw;
            g.globalAlpha = Math.min(1, ly[1]);
            for (var yy = 0; yy < bh; yy += th * 0.82) for (var xx = -off; xx < bw; xx += tw) g.drawImage(ly[0], xx, yy - th * 0.09, tw, th);
        });
        g.globalAlpha = 1; g.globalCompositeOperation = 'destination-out';
        if (o.fadeTop) { var a = g.createLinearGradient(0, 0, 0, o.fadeTop * q); a.addColorStop(0, 'rgba(0,0,0,1)'); a.addColorStop(1, 'rgba(0,0,0,0)'); g.fillStyle = a; g.fillRect(0, 0, bw, o.fadeTop * q); }
        if (o.fadeBot) { var b = g.createLinearGradient(0, bh - o.fadeBot * q, 0, bh); b.addColorStop(0, 'rgba(0,0,0,0)'); b.addColorStop(1, 'rgba(0,0,0,1)'); g.fillStyle = b; g.fillRect(0, bh - o.fadeBot * q, bw, o.fadeBot * q); }
        (o.erase || []).forEach(function (er) {
            var x0 = er[0] * q, x1 = er[1] * q, sf = Math.max(1, er[2] * q), k = er[3];
            if (x1 <= x0) return;
            var gr = g.createLinearGradient(x0 - sf, 0, x1 + sf, 0), span = x1 - x0 + 2 * sf;
            gr.addColorStop(0, 'rgba(0,0,0,0)'); gr.addColorStop(sf / span, 'rgba(0,0,0,' + k + ')');
            gr.addColorStop(1 - sf / span, 'rgba(0,0,0,' + k + ')'); gr.addColorStop(1, 'rgba(0,0,0,0)');
            g.fillStyle = gr; g.fillRect(x0 - sf, 0, span, bh);
        });
        g.globalCompositeOperation = 'source-over';
        ctx.drawImage(fbuf, 0, 0, bw, bh - 0.5, 0, y, L.W, h);
    }
    // Khói bốc lên từ điểm ghim: cuộn nở dần, ấm (ánh lửa) lúc mới bốc rồi nguội thành xám.
    var smokes = [];
    function spawnSmoke(pt, s) {
        smokes.push({ x: pt.x + (Math.random() - 0.5) * 50 * s, y: pt.y - Math.random() * 8 * s, vx: (Math.random() - 0.5) * 10, vy: -(10 + Math.random() * 16), r: (8 + Math.random() * 8) * s, gr: (16 + Math.random() * 14) * s, life: 0, dur: 2.6 + Math.random() * 1.6, sp: Math.random() * 6 });
    }
    function drawSmokes(dt) {
        smokes = smokes.filter(function (p) {
            p.life += dt; if (p.life > p.dur) return false;
            var t = p.life / p.dur;
            p.x += (p.vx + Math.sin(p.life * 1.3 + p.sp) * 6) * dt; p.y += p.vy * dt; p.vy *= 0.995;
            var r = p.r + p.gr * p.life, a = Math.sin(Math.PI * Math.min(1, t * 1.15)) * 0.2;
            var warm = Math.max(0, 1 - t * 2.2);
            var cr = Math.round(130 + 110 * warm), cg = Math.round(132 + 50 * warm), cb = Math.round(150 - 60 * warm);
            var g = ctx.createRadialGradient(p.x, p.y, 0, p.x, p.y, r);
            g.addColorStop(0, 'rgba(' + cr + ',' + cg + ',' + cb + ',' + a.toFixed(3) + ')');
            g.addColorStop(0.6, 'rgba(' + cr + ',' + cg + ',' + cb + ',' + (a * 0.45).toFixed(3) + ')');
            g.addColorStop(1, 'rgba(' + cr + ',' + cg + ',' + cb + ',0)');
            ctx.fillStyle = g; ctx.beginPath(); ctx.arc(p.x, p.y, r, 0, Math.PI * 2); ctx.fill();
            return true;
        });
    }
    // Tia sáng tỏa xuống từ đầu rồng xuyên qua sương (chậm đung đưa, nhịp theo mắt).
    function godRays(time, k) {
        if (k <= 0) return;
        var cx = 860 * L.s, cy = L.top + 95 * L.s, len = 300 * L.s;
        ctx.save(); ctx.globalCompositeOperation = 'lighter';
        for (var i = 0; i < 9; i++) {
            var ang = Math.PI / 2 + (i - 4) * 0.2 + Math.sin(time * 0.25 + i * 1.7) * 0.05, w = (0.035 + 0.025 * hash(i + 3));
            var a = (0.05 + 0.04 * Math.sin(time * 0.8 + i * 2.3)) * k;
            var ex = cx + Math.cos(ang) * len, ey = cy + Math.sin(ang) * len;
            var g = ctx.createLinearGradient(cx, cy, ex, ey);
            g.addColorStop(0, 'rgba(255,200,110,' + a.toFixed(3) + ')'); g.addColorStop(1, 'rgba(255,170,70,0)');
            ctx.fillStyle = g; ctx.beginPath(); ctx.moveTo(cx, cy);
            ctx.lineTo(cx + Math.cos(ang - w) * len, cy + Math.sin(ang - w) * len);
            ctx.lineTo(cx + Math.cos(ang + w) * len, cy + Math.sin(ang + w) * len);
            ctx.closePath(); ctx.fill();
        }
        var hg = ctx.createRadialGradient(cx, cy, 0, cx, cy, 240 * L.s);
        hg.addColorStop(0, 'rgba(255,170,60,' + (0.16 * k).toFixed(3) + ')'); hg.addColorStop(1, 'rgba(255,140,30,0)');
        ctx.fillStyle = hg; ctx.beginPath(); ctx.arc(cx, cy, 240 * L.s, 0, Math.PI * 2); ctx.fill();
        ctx.restore();
    }
    // Sét lóe soi sáng đám mây quanh nó.
    var flashes = [];
    function drawFlashes(dt) {
        if (!flashes.length) return;
        ctx.save(); ctx.globalCompositeOperation = 'lighter';
        flashes = flashes.filter(function (f) {
            f.life += dt; if (f.life > f.dur) return false;
            var a = (1 - f.life / f.dur) * (0.6 + 0.4 * Math.abs(Math.sin(f.life * 40))) * 0.32;
            var g = ctx.createRadialGradient(f.x, f.y, 0, f.x, f.y, f.r);
            g.addColorStop(0, 'rgba(255,214,150,' + a.toFixed(3) + ')'); g.addColorStop(0.5, 'rgba(255,170,80,' + (a * 0.4).toFixed(3) + ')'); g.addColorStop(1, 'rgba(255,150,60,0)');
            ctx.fillStyle = g; ctx.beginPath(); ctx.arc(f.x, f.y, f.r, 0, Math.PI * 2); ctx.fill();
            return true;
        });
        ctx.restore();
    }

    // ===== Bố cục =====
    function layout() {
        VW = document.documentElement.clientWidth; VH = window.innerHeight;
        dpr = Math.min(window.devicePixelRatio || 1, 1.5);
        cv.width = Math.round(VW * dpr); cv.height = Math.round(VH * dpr);
        if (VW < MIN_W) { L = null; crown.style.height = ''; return; }
        var W = VW, s = W / IW;
        var h = Math.round(BAR_T * s) + 'px';
        if (crown.style.height !== h) crown.style.height = h;       // dải trên cao đúng phần đầu rồng của tranh
        var cr = docRect(crown);
        var foot = document.querySelector('.site-footer');
        var fr = foot ? docRect(foot) : { top: document.documentElement.scrollHeight - 220 };
        var wrap = foot && foot.querySelector('.wrap');
        var G = wrap ? docRect(wrap).left + 22 : (W - 1180) / 2 + 22;
        L = { W: W, s: s, top: cr.top, Hb: cr.bottom, Fy: fr.top, G: G, docH: document.documentElement.scrollHeight };
        var gi = Math.round(Math.max(0, G - 8) / s);
        if (alphaC && gi !== L.gutImg) { buildSides(gi); }
        L.gutImg = gi;
    }
    // tọa độ ảnh → tọa độ trang
    function topPt(p) { return { x: p[0] * L.s, y: L.top + p[1] * L.s }; }
    function botPt(p) { return { x: p[0] * L.s, y: L.Fy + (p[1] - BBAR_T) * L.s }; }

    // ===== Hiệu ứng =====
    function burst(pt, n, power) {
        for (var i = 0; i < n; i++) {
            var a = -Math.PI * (0.1 + 0.8 * Math.random()), v = (60 + 220 * Math.random()) * power;
            sparks.push({ x: pt.x + (Math.random() - 0.5) * 16, y: pt.y, vx: Math.cos(a) * v * (Math.random() < 0.5 ? -1 : 1), vy: Math.sin(a) * v, life: 0, dur: 0.5 + Math.random() * 0.7, r: 0.8 + Math.random() * 1.8 });
        }
    }
    function drawSparks(dt) {
        ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.lineCap = 'round';
        sparks = sparks.filter(function (p) {
            p.life += dt; if (p.life > p.dur) return false;
            p.vy += 420 * dt; p.vx *= 0.985; p.x += p.vx * dt; p.y += p.vy * dt;
            var a = 1 - p.life / p.dur;
            ctx.strokeStyle = 'rgba(255,214,120,' + a.toFixed(3) + ')'; ctx.lineWidth = p.r;
            ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(p.x - p.vx * 0.025, p.y - p.vy * 0.025); ctx.stroke();
            return true;
        });
        ctx.restore();
    }
    // Điểm ghim: quầng nứt phập phồng + lóe sáng và vòng sóng khi vừa đập xuống (flash 1 → 0).
    function anchorGlow(pt, time, flash, seed) {
        var s = L.s, pulse = 0.55 + 0.45 * Math.sin(time * 1.6 + seed * 1.7), R = 70 * s;
        ctx.save(); ctx.globalCompositeOperation = 'lighter';
        var g = ctx.createRadialGradient(pt.x, pt.y, 0, pt.x, pt.y, R);
        g.addColorStop(0, 'rgba(255,200,90,' + (0.22 * pulse + 0.7 * flash).toFixed(3) + ')');
        g.addColorStop(0.4, 'rgba(255,150,40,' + (0.10 * pulse + 0.35 * flash).toFixed(3) + ')');
        g.addColorStop(1, 'rgba(255,120,20,0)');
        ctx.fillStyle = g; ctx.beginPath(); ctx.arc(pt.x, pt.y, R, 0, Math.PI * 2); ctx.fill();
        if (flash > 0.01) {
            var rr = (1 - flash) * 150 * s + 10;
            ctx.strokeStyle = 'rgba(255,226,150,' + (flash * 0.8).toFixed(3) + ')'; ctx.lineWidth = 3 * s * flash + 0.5;
            ctx.beginPath(); ctx.ellipse(pt.x, pt.y, rr, rr * 0.35, 0, 0, Math.PI * 2); ctx.stroke();
        }
        ctx.restore();
    }
    function eyeGlow(time, k) {
        if (k <= 0) return;
        ctx.save(); ctx.globalCompositeOperation = 'lighter';
        EYES.forEach(function (e, i) {
            var p = topPt(e), pulse = 0.7 + 0.3 * Math.sin(time * 2.1 + i * 0.6), R = 26 * L.s;
            var g = ctx.createRadialGradient(p.x, p.y, 0, p.x, p.y, R);
            g.addColorStop(0, 'rgba(255,190,70,' + (0.75 * k * pulse).toFixed(3) + ')');
            g.addColorStop(0.35, 'rgba(255,120,20,' + (0.35 * k * pulse).toFixed(3) + ')');
            g.addColorStop(1, 'rgba(255,80,0,0)');
            ctx.fillStyle = g; ctx.beginPath(); ctx.arc(p.x, p.y, R, 0, Math.PI * 2); ctx.fill();
        });
        ctx.restore();
    }
    // Vệt ánh kim lướt chéo trên thân rồng: dải sáng chỉ tô lên phần có rồng (lớp tách nền làm mặt nạ).
    function glint(time) {
        var u = (time % 7) / 2.8;
        if (u > 1) return;
        var g = glintC.getContext('2d'), H = glintC.height;
        g.globalCompositeOperation = 'source-over'; g.clearRect(0, 0, IW, H);
        g.drawImage(alphaC, 0, 0, IW, H, 0, 0, IW, H);
        g.globalCompositeOperation = 'source-in';
        var x = -300 + u * (IW + 600), gr = g.createLinearGradient(x - 160, 0, x + 160, H * 0.4);
        gr.addColorStop(0, 'rgba(255,240,200,0)'); gr.addColorStop(0.5, 'rgba(255,246,215,.85)'); gr.addColorStop(1, 'rgba(255,240,200,0)');
        g.fillStyle = gr; g.fillRect(0, 0, IW, H);
        ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = 0.45;
        ctx.drawImage(glintC, 0, 0, IW, H, 0, L.top, L.W, H * L.s);
        ctx.restore();
    }
    function spawnBolt(x, y, dx, dy, len) {
        flashes.push({ x: x + dx * len * 0.4, y: y + dy * len * 0.4, r: len * 2.2, life: 0, dur: 0.45 });
        var l = Math.hypot(dx, dy) || 1, d = { x: dx / l, y: dy / l };
        function seg(x0, y0, dir, Ln, n) {
            var pp = { x: -dir.y, y: dir.x }, pts = [{ x: x0, y: y0 }];
            for (var i = 1; i <= n; i++) { var j = (Math.random() - 0.5) * Ln * 0.2; pts.push({ x: x0 + dir.x * Ln * i / n + pp.x * j, y: y0 + dir.y * Ln * i / n + pp.y * j }); }
            return pts;
        }
        var main = seg(x, y, d, len, 10), lines = [main];
        for (var b = 0; b < 2; b++) {
            var p = main[3 + Math.floor(Math.random() * 5)], a = (Math.random() < 0.5 ? -1 : 1) * (0.45 + Math.random() * 0.4);
            lines.push(seg(p.x, p.y, { x: d.x * Math.cos(a) - d.y * Math.sin(a), y: d.x * Math.sin(a) + d.y * Math.cos(a) }, len * 0.35, 5));
        }
        bolts.push({ lines: lines, life: 0, dur: 0.3 + Math.random() * 0.25 });
    }
    function drawBolts(dt) {
        if (!bolts.length) return;
        ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.lineJoin = 'round'; ctx.lineCap = 'round';
        bolts = bolts.filter(function (b) {
            b.life += dt; if (b.life > b.dur) return false;
            var a = (1 - b.life / b.dur) * (0.55 + 0.45 * Math.abs(Math.sin(b.life * 55)));
            b.lines.forEach(function (ps, k) {
                ctx.beginPath(); ctx.moveTo(ps[0].x, ps[0].y);
                for (var i = 1; i < ps.length; i++) ctx.lineTo(ps[i].x, ps[i].y);
                ctx.shadowColor = 'rgba(255,180,60,1)'; ctx.shadowBlur = 16;
                ctx.strokeStyle = 'rgba(255,190,80,' + (a * 0.5).toFixed(3) + ')'; ctx.lineWidth = k ? 2.5 : 4.5; ctx.stroke();
                ctx.shadowBlur = 0;
                ctx.strokeStyle = 'rgba(255,252,232,' + a.toFixed(3) + ')'; ctx.lineWidth = k ? 0.8 : 1.4; ctx.stroke();
            });
            return true;
        });
        ctx.restore();
    }
    function drawEmbers(dt) {
        ctx.save(); ctx.globalCompositeOperation = 'lighter';
        embers = embers.filter(function (e) {
            e.life += dt; if (e.life > e.dur) return false;
            e.x += e.vx * dt + Math.sin(e.life * 3 + e.r) * 0.3; e.y += e.vy * dt;
            ctx.fillStyle = 'rgba(255,192,95,' + (Math.sin(Math.PI * e.life / e.dur) * 0.9).toFixed(3) + ')';
            ctx.beginPath(); ctx.arc(e.x, e.y, e.r, 0, Math.PI * 2); ctx.fill();
            return true;
        });
        ctx.restore();
    }

    // ===== Khung hình =====
    function topVisible(sy) { return L.Hb + 520 * L.s - sy > 0; }
    function botVisible(sy) { return L.Fy + (IH - BBAR_T) * L.s > sy && L.Fy - (BBAR_T - SIDE_B[0]) * L.s < sy + VH; }
    function render(now, dt) {
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.clearRect(0, 0, cv.width, cv.height);
        if (!L || !alphaC) return;
        var time = now / 1000, sy = window.scrollY, W = L.W, s = L.s, d = dpr;
        if (t0 < 0) t0 = time;
        var e = reduce ? 9 : time - t0;
        var navEl = document.querySelector('.etoms-nav');
        var nb = navEl ? navEl.getBoundingClientRect() : { top: L.Hb - sy, bottom: L.Hb - sy + 60 };
        var stuck = nb.top > L.Hb - sy + 1;                       // menu dính đầu trang khi cuộn → không vẽ đè menu
        var gut = Math.max(0, L.G - 8);                            // lề trống hai bên nội dung

        if (topVisible(sy)) {
            // mở màn: tranh hiện ra từ sương (0–1,2s), móng đập xuống mép lúc 1,2s (lóe + tia lửa + rung)
            var fade = reduce ? 1 : easeOut(e / 1.2);
            if (!impactsT && e >= 1.2) { impactsT = true; shakeT = reduce ? 0 : 1; if (!reduce) CLAW_T.forEach(function (c) { burst(topPt(c), 26, 1); }); }
            var flashT = (reduce || e < 1.2) ? 0 : clamp(1 - (e - 1.2) / 0.7, 0, 1);
            shakeT = Math.max(0, shakeT - dt * 2.6);
            var jx = shakeT * (Math.random() - 0.5) * 6 * s, jy = shakeT * (Math.random() - 0.5) * 4 * s;

            ctx.save();
            ctx.setTransform(d, 0, 0, d, 0, 0);
            ctx.beginPath(); ctx.rect(0, 0, W, stuck ? nb.top : VH); ctx.clip();
            ctx.setTransform(d, 0, 0, d, jx * d, (jy - sy) * d);
            ctx.globalAlpha = fade;
            ctx.drawImage(img, 0, 0, IW, BAR_T, 0, L.top, W, BAR_T * s);                              // dải đầu rồng
            ctx.drawImage(alphaC, 0, BAR_T, IW, BAR_OVER, 0, L.top + BAR_T * s, W, BAR_OVER * s);    // móng + cằm đè menu
            // thân hai bên (đã mờ dần trước mép nội dung)
            ctx.drawImage(sideU, 0, 0, IW, sideU.height, 0, L.top + SIDE_U[0] * s, W, sideU.height * s);
            ctx.globalAlpha = 1;
            var eyeK = reduce ? 1 : clamp((e - 1.3) / 0.6, 0, 1);
            // Sương cuộn: lớp sau (dày ở hai bên, mỏng ở giữa cho đầu rồng nổi lên), mở màn đặc rồi tan dần
            fogBand(L.top, L.Hb - L.top + 40 * s, time, {
                a1: 0.55 + 0.45 * (1 - fade), a2: 0.4 + 0.5 * (1 - fade), sp1: 9, sp2: -17, fadeTop: 0, fadeBot: 40 * s,
                erase: [[W * 0.38, W * 0.62, W * 0.12, 0.78 * fade]]
            });
            godRays(time, eyeK);
            if (!reduce) glint(time);
            eyeGlow(time, eyeK);
            CLAW_T.forEach(function (c, i) { anchorGlow(topPt(c), time, flashT, i); });
            // sương tràn xuống mép trên banner bên dưới menu + cuộn dọc hai lề (khoét vùng nội dung, mép mềm)
            fogBand(L.Hb + 50 * s, 170 * s, time + 3, { a1: 0.32, a2: 0.22, sp1: 6, sp2: -11, fadeTop: 60 * s, fadeBot: 100 * s, erase: [[gut + 40, W - gut - 40, 120, 0.7]] });
            fogBand(L.Hb + 150 * s, 480 * s, time + 7, { a1: 0.5, a2: 0.35, sp1: -5, sp2: 9, fadeTop: 110 * s, fadeBot: 160 * s, erase: [[gut - 20, W - gut + 20, 70, 1]] });
            ctx.restore();
        }

        if (botVisible(sy)) {
            if (botT0 < 0) botT0 = time;
            var eb = reduce ? 9 : time - botT0, fb = reduce ? 1 : easeOut(eb / 1.0);
            if (!impactsB && eb >= 0.9) { impactsB = true; shakeB = reduce ? 0 : 1; if (!reduce) CLAW_B.forEach(function (c) { burst(botPt(c), 22, 0.9); }); }
            var flashB = (reduce || eb < 0.9) ? 0 : clamp(1 - (eb - 0.9) / 0.7, 0, 1);
            shakeB = Math.max(0, shakeB - dt * 2.6);
            var bx = shakeB * (Math.random() - 0.5) * 6 * s, by = shakeB * (Math.random() - 0.5) * 4 * s;
            ctx.save();
            ctx.setTransform(d, 0, 0, d, bx * d, (by - sy) * d);
            ctx.globalAlpha = fb;
            // thân hai bên + móng + mây dưới (đã mờ dần trước mép nội dung, móng đè mép Footer ở hai góc)
            var y0 = L.Fy + (SIDE_B[0] - BBAR_T) * s;
            ctx.drawImage(sideB, 0, 0, IW, sideB.height, 0, y0, W, sideB.height * s);
            ctx.globalAlpha = 1;
            CLAW_B.forEach(function (c, i) { anchorGlow(botPt(c), time, flashB, i + 2); });
            // sương cuộn quanh hai góc dưới: dọc lề phía trên Footer, và tràn qua mép Footer ở hai góc
            fogBand(L.Fy - 360 * s, 380 * s, time + 11, { a1: 0.5 + 0.4 * (1 - fb), a2: 0.35, sp1: 7, sp2: -12, fadeTop: 150 * s, fadeBot: 60 * s, erase: [[gut - 20, W - gut + 20, 70, 1]] });
            fogBand(L.Fy - 20 * s, Math.min(L.docH - L.Fy + 20 * s, 230 * s), time + 5, { a1: 0.55, a2: 0.4, sp1: -6, sp2: 10, fadeTop: 50 * s, fadeBot: 40 * s, erase: [[SIDE_W * s * 0.85, W - SIDE_W * s * 0.85, 90 * s, 1]] });
            ctx.restore();
        }

        if (!reduce) {
            nextBolt -= dt;
            if (nextBolt <= 0) {
                nextBolt = 1.2 + Math.random() * 2.4;
                var spots = [];
                if (topVisible(sy) && e > 1.5) spots.push([70 * s, L.top + 60 * s, 1, 0.6], [W - 70 * s, L.top + 60 * s, -1, 0.6]);
                if (botVisible(sy)) spots.push([80 * s, L.Fy + 110 * s, 1, -0.7], [W - 80 * s, L.Fy + 110 * s, -1, -0.7]);
                if (spots.length) {
                    var sp = spots[Math.floor(Math.random() * spots.length)];
                    spawnBolt(sp[0], sp[1], sp[2] * (0.6 + Math.random() * 0.7), sp[3] + (Math.random() - 0.5) * 0.6, (70 + Math.random() * 80) * s);
                }
            }
            if (smokes.length < 70 && Math.random() < dt * 9) {          // khói bốc từ 4 điểm ghim
                var sk = Math.random(), sp2 = null;
                if (topVisible(sy) && e > 1.2 && sk < 0.5) sp2 = topPt(CLAW_T[sk < 0.25 ? 0 : 1]);
                else if (botVisible(sy) && botT0 >= 0 && time - botT0 > 0.9) sp2 = botPt(CLAW_B[sk < 0.75 ? 0 : 1]);
                if (sp2) spawnSmoke(sp2, s);
            }
            if (embers.length < 50 && Math.random() < dt * 8) {
                var pk = Math.random(), src = null;
                if (topVisible(sy) && pk < 0.5) src = topPt(CLAW_T[pk < 0.25 ? 0 : 1]);
                else if (botVisible(sy)) src = botPt(CLAW_B[pk < 0.75 ? 0 : 1]);
                if (src) embers.push({ x: src.x + (Math.random() - 0.5) * 90 * s, y: src.y - Math.random() * 30 * s, vx: (Math.random() - 0.5) * 14, vy: -14 - Math.random() * 22, life: 0, dur: 1.6 + Math.random() * 1.8, r: 0.7 + Math.random() * 1.6 });
            }
            ctx.save();
            ctx.setTransform(d, 0, 0, d, 0, 0);
            if (stuck) { ctx.beginPath(); ctx.rect(0, nb.bottom, W, VH); ctx.clip(); }
            ctx.setTransform(d, 0, 0, d, 0, -sy * d);
            drawFlashes(dt);
            drawSmokes(dt);
            drawBolts(dt);
            drawSparks(dt);
            drawEmbers(dt);
            ctx.restore();
        }
    }

    var idleCleared = false;
    function loop(now) {
        if (!running) return;
        var dt = lastNow ? Math.min(0.05, (now - lastNow) / 1000) : 0.016;
        lastNow = now;
        var sy = window.scrollY;
        if (L && alphaC && (topVisible(sy) || botVisible(sy) || sparks.length)) { render(now, dt); idleCleared = false; }
        else if (!idleCleared) { ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.clearRect(0, 0, cv.width, cv.height); idleCleared = true; }
        raf = requestAnimationFrame(loop);
    }
    function play() { if (running || reduce) return; running = true; lastNow = 0; raf = requestAnimationFrame(loop); }
    function pause() { running = false; if (raf) cancelAnimationFrame(raf); raf = 0; }

    var sig = '';
    function signature() {
        var f = document.querySelector('.site-footer');
        return [document.documentElement.clientWidth, window.innerHeight, f ? Math.round(docRect(f).top) : 0, document.documentElement.scrollHeight].join('|');
    }
    function relayoutIfNeeded(force) {
        var sg = signature();
        if (!force && sg === sig) return;
        layout(); sig = signature();
        if (reduce) render(performance.now(), 0);
    }
    function start() {
        relayoutIfNeeded(true);
        window.addEventListener('resize', function () { relayoutIfNeeded(true); });
        window.addEventListener('scroll', function () { if (reduce) render(performance.now(), 0); }, { passive: true });
        document.addEventListener('visibilitychange', function () { if (document.hidden) pause(); else play(); });
        setInterval(function () { relayoutIfNeeded(false); }, 900);   // tin tức… tải sau làm Footer dời xuống
        play();
    }
    layout();                                                          // đặt chiều cao dải trên ngay (tránh trang nhảy)
    img.onload = function () { buildLayers(); if (document.readyState === 'complete') start(); else window.addEventListener('load', start); };
    img.src = SRC;
})();
