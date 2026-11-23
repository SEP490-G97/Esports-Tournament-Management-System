// Hiệu ứng 3D cho hero trang chủ: rồng vàng bay lượn (Three.js, thân rồng dựng từ hình học cơ bản,
// không cần model ngoài), hạt lửa theo đuôi, sao nền, camera lắc theo chuột, ô thống kê nghiêng 3D.
// Tự tắt khi: không có WebGL, người dùng bật "giảm chuyển động", hoặc hero cuộn khỏi màn hình.

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

// ===== Rồng bay 3D =====
(function () {
    var hero = document.getElementById('heroCarousel');
    var canvas = document.getElementById('hero-3d');
    if (!hero || !canvas || typeof THREE === 'undefined') return;
    if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

    var renderer;
    try {
        renderer = new THREE.WebGLRenderer({ canvas: canvas, alpha: true, antialias: true, powerPreference: 'high-performance' });
    } catch (e) { return; }
    renderer.outputEncoding = THREE.sRGBEncoding;
    renderer.setClearColor(0x000000, 0);

    var isMobile = window.innerWidth < 720;
    var SEGMENTS = isMobile ? 46 : 72;
    var PARTICLES = isMobile ? 160 : 380;
    var STARS = isMobile ? 220 : 520;

    var scene = new THREE.Scene();
    var camera = new THREE.PerspectiveCamera(42, 2, 0.1, 100);
    camera.position.set(0, 0.4, 13);
    var camTarget = new THREE.Vector3(0, 0.3, 0);

    // --- Ánh sáng ---
    scene.add(new THREE.AmbientLight(0x6b7bb0, 0.7));
    var sun = new THREE.DirectionalLight(0x5f7dff, 0.9);
    sun.position.set(-6, 8, 6);
    scene.add(sun);
    var rim = new THREE.PointLight(0x4fd4ff, 1.1, 24);
    rim.position.set(7, -3, 3);
    scene.add(rim);
    var headLight = new THREE.PointLight(0xffc96b, 1.3, 7);
    scene.add(headLight);

    // --- Texture quầng sáng (canvas 2D) ---
    function glowTexture() {
        var c = document.createElement('canvas');
        c.width = c.height = 64;
        var g = c.getContext('2d');
        var grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
        grad.addColorStop(0, 'rgba(255,255,255,1)');
        grad.addColorStop(0.35, 'rgba(255,255,255,.55)');
        grad.addColorStop(1, 'rgba(255,255,255,0)');
        g.fillStyle = grad;
        g.fillRect(0, 0, 64, 64);
        var t = new THREE.CanvasTexture(c);
        return t;
    }
    var glowTex = glowTexture();

    // --- Đường bay: vòng ∞ nghiêng, trải rộng toàn hero ---
    // Quỹ đạo tính theo khung hero tỉ lệ ~2.7:1 (camera z=13, fov 42): bổ nhào từ dải trên (phía xa) xuống
    // lượn quanh ô thống kê bên phải (gần camera), vòng ra mép phải rồi bay ngược về theo dải trên cùng.
    // Nửa trái dưới (tiêu đề, nút) để trống. Khung hẹp hơn thì co trục x để rồng vẫn nằm trong hình.
    var basePoints = [
        [-4.2, 5.5, -4.0],
        [1.4, 3.1, 0.0],
        [6.7, 0.7, 1.5],
        [10.4, -1.8, 0.5],
        [14.8, 0.9, -2.0],
        [14.3, 5.5, -5.0],
        [4.3, 6.9, -7.5],
        [-12.3, 6.1, -8.0]
    ];
    var path = null, pathScale = 0;
    function buildPath(aspect) {
        var xs = Math.min(1, aspect / 2.6);
        if (Math.abs(xs - pathScale) < 0.03 && path) return;
        pathScale = xs;
        path = new THREE.CatmullRomCurve3(basePoints.map(function (p) {
            return new THREE.Vector3(p[0] * xs, p[1] * (0.85 + 0.15 * xs), p[2]);
        }), true, 'catmullrom', 0.55);
    }
    buildPath(2.7);

    // --- Vật liệu ---
    var matBody = new THREE.MeshStandardMaterial({ color: 0xc98f1c, metalness: 0.85, roughness: 0.3, emissive: 0x3a2203, emissiveIntensity: 0.45 });
    var matBelly = new THREE.MeshStandardMaterial({ color: 0xe3b24a, metalness: 0.7, roughness: 0.38, emissive: 0x2e1d05, emissiveIntensity: 0.35 });
    var matFin = new THREE.MeshStandardMaterial({ color: 0x2744d8, metalness: 0.2, roughness: 0.55, emissive: 0x1230e0, emissiveIntensity: 1.1, transparent: true, opacity: 0.9, side: THREE.DoubleSide });
    var matEye = new THREE.MeshBasicMaterial({ color: 0x9bf3ff });
    var matHorn = new THREE.MeshStandardMaterial({ color: 0xf0cf7a, metalness: 0.6, roughness: 0.45, emissive: 0x4a3208, emissiveIntensity: 0.4 });

    var geoBody = new THREE.SphereGeometry(1, 14, 12);
    var geoFin = new THREE.ConeGeometry(0.5, 1, 4);
    var geoHorn = new THREE.ConeGeometry(0.16, 0.9, 6);

    // --- Thân rồng: mỗi đốt là một Group (quay theo đường bay), bên trong là ellipsoid + vây lưng ---
    var dragon = new THREE.Group();
    scene.add(dragon);
    var segs = [];
    for (var i = 0; i < SEGMENTS; i++) {
        var k = i / (SEGMENTS - 1);
        var r = 0.46 * Math.pow(1 - k, 0.8) + 0.06;
        var g = new THREE.Group();
        var body = new THREE.Mesh(geoBody, i % 3 === 0 ? matBelly : matBody);
        body.scale.set(r, r * 0.9, r * 1.7);
        g.add(body);
        if (i > 3 && i % 3 === 0 && i < SEGMENTS - 5) {
            var fin = new THREE.Mesh(geoFin, matFin);
            var fh = 0.5 * (1 - k) + 0.1;
            fin.scale.set(r * 0.5, fh, r * 0.3);
            fin.position.set(0, r * 0.8 + fh * 0.45, 0);
            fin.rotation.y = Math.PI / 4;
            fin.rotation.x = -0.35;
            g.add(fin);
        }
        if (i === 10 || i === 26) {
            // Chân nhỏ hai bên, xòe ra như vây
            var pawL = new THREE.Mesh(geoFin, matFin), pawR = new THREE.Mesh(geoFin, matFin);
            pawL.scale.set(r * 0.35, r * 1.6, r * 0.35); pawR.scale.copy(pawL.scale);
            pawL.position.set(-r * 0.9, -r * 0.5, 0); pawR.position.set(r * 0.9, -r * 0.5, 0);
            pawL.rotation.z = 0.9; pawR.rotation.z = -0.9;
            g.add(pawL); g.add(pawR);
        }
        dragon.add(g);
        segs.push({ g: g, r: r });
    }

    // --- Đầu rồng: mõm, sừng, mắt, râu, bờm ---
    var head = segs[0].g;
    var hr = segs[0].r;
    var snout = new THREE.Mesh(geoBody, matBody);
    snout.scale.set(hr * 0.72, hr * 0.55, hr * 1.1);
    snout.position.set(0, -hr * 0.15, hr * 1.35);
    head.add(snout);
    var jaw = new THREE.Mesh(geoBody, matBelly);
    jaw.scale.set(hr * 0.6, hr * 0.28, hr * 0.95);
    jaw.position.set(0, -hr * 0.5, hr * 1.2);
    head.add(jaw);
    [-1, 1].forEach(function (s) {
        var horn = new THREE.Mesh(geoHorn, matHorn);
        horn.scale.setScalar(hr * 1.2);
        horn.position.set(s * hr * 0.45, hr * 0.75, -hr * 0.35);
        horn.rotation.x = -0.9; horn.rotation.z = -s * 0.35;
        head.add(horn);
        var eye = new THREE.Mesh(geoBody, matEye);
        eye.scale.setScalar(hr * 0.13);
        eye.position.set(s * hr * 0.5, hr * 0.22, hr * 0.85);
        head.add(eye);
        var eyeGlow = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex, color: 0x7fe9ff, transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false }));
        eyeGlow.scale.setScalar(hr * 0.75);
        eyeGlow.position.copy(eye.position);
        head.add(eyeGlow);
        // Râu: ống cong mảnh
        var whisker = new THREE.Mesh(new THREE.TubeGeometry(new THREE.QuadraticBezierCurve3(
            new THREE.Vector3(s * hr * 0.35, -hr * 0.2, hr * 1.9),
            new THREE.Vector3(s * hr * 1.6, -hr * 0.1, hr * 1.4),
            new THREE.Vector3(s * hr * 2.3, hr * 0.5, hr * 0.2)), 12, hr * 0.035, 5, false), matHorn);
        head.add(whisker);
        // Bờm sau gáy
        for (var m = 0; m < 3; m++) {
            var mane = new THREE.Mesh(geoFin, matFin);
            mane.scale.set(hr * 0.3, hr * 1.1 - m * 0.08, hr * 0.3);
            mane.position.set(s * hr * (0.25 + m * 0.3), hr * 0.5 - m * 0.12, -hr * 0.7);
            mane.rotation.x = -1.3; mane.rotation.z = -s * (0.4 + m * 0.25);
            head.add(mane);
        }
    });
    var headHalo = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex, color: 0xffcf6e, transparent: true, opacity: 0.32, blending: THREE.AdditiveBlending, depthWrite: false }));
    headHalo.scale.setScalar(3.2);
    scene.add(headHalo);

    // --- Model rồng thật (.glb) nếu có: canvas data-model="/models/dragon.glb". Nạp xong thì ẩn rồng dựng tay.
    // data-forward: trục mặt trước của model (z | -z | x | -x), data-size: chiều dài mong muốn (đơn vị cảnh).
    var modelPivot = null, mixer = null, modelBack = new THREE.Vector3();
    (function loadModel() {
        var url = canvas.getAttribute('data-model');
        if (!url || typeof THREE.GLTFLoader === 'undefined') return;
        fetch(url, { method: 'HEAD' }).then(function (res) {
            if (!res.ok) return;
            var loader = new THREE.GLTFLoader();
            if (typeof THREE.DRACOLoader !== 'undefined')
                loader.setDRACOLoader(new THREE.DRACOLoader().setDecoderPath('https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/libs/draco/'));
            loader.load(url, function (gltf) {
                var model = gltf.scene;
                var box = new THREE.Box3().setFromObject(model);
                var size = box.getSize(new THREE.Vector3());
                var target = parseFloat(canvas.getAttribute('data-size')) || 5;
                model.scale.setScalar(target / Math.max(size.x, size.y, size.z));
                box.setFromObject(model);
                model.position.sub(box.getCenter(new THREE.Vector3()));
                var inner = new THREE.Group();
                inner.add(model);
                switch ((canvas.getAttribute('data-forward') || 'z').toLowerCase()) {
                    case '-z': inner.rotation.y = Math.PI; break;
                    case 'x': inner.rotation.y = -Math.PI / 2; break;
                    case '-x': inner.rotation.y = Math.PI / 2; break;
                }
                modelPivot = new THREE.Group();
                modelPivot.add(inner);
                scene.add(modelPivot);
                if (gltf.animations && gltf.animations.length) {
                    mixer = new THREE.AnimationMixer(model);
                    var clip = gltf.animations.find(function (a) { return /fly|idle|flap/i.test(a.name); }) || gltf.animations[0];
                    mixer.clipAction(clip).play();
                }
                dragon.visible = false;
            }, undefined, function () { /* lỗi nạp model: giữ rồng dựng tay */ });
        }).catch(function () { /* không có file: giữ rồng dựng tay */ });
    })();

    // --- Hạt lửa theo thân/đuôi ---
    var pGeo = new THREE.BufferGeometry();
    var pPos = new Float32Array(PARTICLES * 3), pCol = new Float32Array(PARTICLES * 3);
    var pVel = new Float32Array(PARTICLES * 3), pLife = new Float32Array(PARTICLES), pMax = new Float32Array(PARTICLES);
    for (var p = 0; p < PARTICLES; p++) { pLife[p] = -Math.random() * 2; pPos[p * 3 + 1] = -100; }
    pGeo.setAttribute('position', new THREE.BufferAttribute(pPos, 3));
    pGeo.setAttribute('color', new THREE.BufferAttribute(pCol, 3));
    var pMat = new THREE.PointsMaterial({ size: 0.17, map: glowTex, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, vertexColors: true, sizeAttenuation: true });
    scene.add(new THREE.Points(pGeo, pMat));
    var emberA = new THREE.Color(0xffd27a), emberB = new THREE.Color(0x7fb3ff);

    // --- Sao nền trôi chậm ---
    var sGeo = new THREE.BufferGeometry();
    var sPos = new Float32Array(STARS * 3);
    for (var s = 0; s < STARS; s++) {
        sPos[s * 3] = (Math.random() - 0.5) * 36;
        sPos[s * 3 + 1] = (Math.random() - 0.5) * 16;
        sPos[s * 3 + 2] = -4 - Math.random() * 14;
    }
    sGeo.setAttribute('position', new THREE.BufferAttribute(sPos, 3));
    var sMat = new THREE.PointsMaterial({ size: 0.07, color: 0x9db0ff, map: glowTex, transparent: true, opacity: 0.75, depthWrite: false, blending: THREE.AdditiveBlending });
    var stars = new THREE.Points(sGeo, sMat);
    scene.add(stars);

    // --- Chế độ sáng: bớt cộng sáng để không bị trắng xóa ---
    function applyTheme() {
        var light = document.documentElement.getAttribute('data-theme') === 'light';
        sMat.opacity = light ? 0.35 : 0.75;
        sMat.color.set(light ? 0x3f5ff5 : 0x9db0ff);
        pMat.blending = light ? THREE.NormalBlending : THREE.AdditiveBlending;
        pMat.needsUpdate = true;
        headHalo.material.opacity = light ? 0.18 : 0.32;
        scene.children.forEach(function (c) { if (c.isAmbientLight) c.intensity = light ? 1.1 : 0.7; });
    }
    applyTheme();
    if ('MutationObserver' in window)
        new MutationObserver(applyTheme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });

    // --- Kích thước ---
    function resize() {
        var w = hero.clientWidth, h = hero.clientHeight;
        if (!w || !h) return;
        renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, isMobile ? 1.25 : 1.75));
        renderer.setSize(w, h, false);
        camera.aspect = w / h;
        camera.fov = w / h < 1.1 ? 58 : 42;
        camera.updateProjectionMatrix();
        buildPath(w / h);
    }
    resize();
    if ('ResizeObserver' in window) new ResizeObserver(resize).observe(hero); else window.addEventListener('resize', resize);

    // --- Chuột: camera lắc nhẹ, rồng ngẩng theo ---
    var mouse = { x: 0, y: 0 };
    hero.addEventListener('mousemove', function (e) {
        var r = hero.getBoundingClientRect();
        mouse.x = ((e.clientX - r.left) / r.width - 0.5) * 2;
        mouse.y = ((e.clientY - r.top) / r.height - 0.5) * 2;
    });
    hero.addEventListener('mouseleave', function () { mouse.x = 0; mouse.y = 0; });

    // --- Vòng vẽ ---
    var clock = new THREE.Clock();
    var t = 0, running = false, raf = 0;
    var SPACING = isMobile ? 0.0058 : 0.0038;
    var tmp = new THREE.Vector3(), ahead = new THREE.Vector3();

    function frame() {
        if (!running) return;
        raf = requestAnimationFrame(frame);
        var dt = Math.min(clock.getDelta(), 0.05);
        var time = clock.elapsedTime;
        t = (t + dt * 0.052) % 1;

        for (var i = 0; i < SEGMENTS; i++) {
            var u = ((t - i * SPACING) % 1 + 1) % 1;
            var seg = segs[i];
            path.getPointAt(u, seg.g.position);
            // Gợn sóng dọc thân, mạnh dần về đuôi
            var wave = Math.sin(time * 3.2 - i * 0.38) * 0.14 * (0.25 + 0.75 * i / SEGMENTS);
            seg.g.position.y += wave;
            seg.g.position.x += Math.cos(time * 2.4 - i * 0.31) * 0.06 * (i / SEGMENTS);
            if (i === 0) {
                path.getPointAt((u + 0.012) % 1, ahead);
                ahead.y += Math.sin(time * 3.2 + 0.4) * 0.14 * 0.25;
                seg.g.lookAt(ahead);
            } else {
                seg.g.lookAt(segs[i - 1].g.position);
            }
        }
        // Model rồng thật: di chuyển theo đường bay, cập nhật animation
        if (modelPivot) {
            path.getPointAt(t, modelPivot.position);
            modelPivot.position.y += Math.sin(time * 2.2) * 0.25;
            path.getPointAt((t + 0.015) % 1, ahead);
            ahead.y += Math.sin(time * 2.2 + 0.3) * 0.25;
            modelPivot.lookAt(ahead);
            if (mixer) mixer.update(dt);
            modelPivot.getWorldDirection(modelBack).multiplyScalar(-1);
        }
        var headPos = modelPivot ? modelPivot.position : head.position;
        headLight.position.copy(headPos);
        headLight.intensity = 1.2 + Math.sin(time * 5) * 0.3;
        headHalo.position.copy(headPos);
        headHalo.material.opacity = (document.documentElement.getAttribute('data-theme') === 'light' ? 0.18 : 0.3) + Math.sin(time * 5) * 0.05;

        // Hạt lửa
        for (var p = 0; p < PARTICLES; p++) {
            var o = p * 3;
            pLife[p] -= dt;
            if (pLife[p] <= 0) {
                var spread;
                if (modelPivot) {
                    tmp.copy(modelPivot.position).addScaledVector(modelBack, Math.random() * 2.5);
                    spread = 0.9;
                } else {
                    var src = segs[6 + Math.floor(Math.random() * (SEGMENTS - 6))];
                    tmp.copy(src.g.position);
                    spread = src.r;
                }
                pPos[o] = tmp.x + (Math.random() - 0.5) * spread;
                pPos[o + 1] = tmp.y + (Math.random() - 0.5) * spread;
                pPos[o + 2] = tmp.z + (Math.random() - 0.5) * spread;
                pVel[o] = (Math.random() - 0.5) * 0.35;
                pVel[o + 1] = 0.25 + Math.random() * 0.45;
                pVel[o + 2] = (Math.random() - 0.5) * 0.35;
                pMax[p] = pLife[p] = 0.8 + Math.random() * 1.3;
            } else {
                pPos[o] += pVel[o] * dt;
                pPos[o + 1] += pVel[o + 1] * dt;
                pPos[o + 2] += pVel[o + 2] * dt;
                pVel[o + 1] -= 0.12 * dt;
            }
            var f = Math.max(pLife[p] / pMax[p], 0);
            var c = (p % 5 === 0) ? emberB : emberA;
            pCol[o] = c.r * f; pCol[o + 1] = c.g * f; pCol[o + 2] = c.b * f;
        }
        pGeo.attributes.position.needsUpdate = true;
        pGeo.attributes.color.needsUpdate = true;

        stars.rotation.y = time * 0.012;
        stars.position.x = mouse.x * -0.6;

        camera.position.x += (mouse.x * 1.4 - camera.position.x) * 0.045;
        camera.position.y += (0.4 - mouse.y * 0.7 - camera.position.y) * 0.045;
        camera.lookAt(camTarget);

        renderer.render(scene, camera);
    }

    function start() { if (running) return; running = true; clock.start(); frame(); }
    function stop() { running = false; if (raf) cancelAnimationFrame(raf); raf = 0; }

    var visible = true;
    if ('IntersectionObserver' in window) {
        new IntersectionObserver(function (es) {
            visible = es[0].isIntersecting;
            if (visible && !document.hidden) start(); else stop();
        }, { threshold: 0.05 }).observe(hero);
    } else start();
    document.addEventListener('visibilitychange', function () { if (document.hidden) stop(); else if (visible) start(); });
    hero.classList.add('hero-3d-on');
})();
