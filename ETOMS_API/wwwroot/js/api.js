// Bọc fetch() theo đúng envelope JSON của ApiControllerBase.FromResult():
//  - Thành công: { message, data? }
//  - Thất bại:   { message, fieldErrors: { Field: "msg" } }   (lỗi ném ra có .status/.message/.fieldErrors)
// Đăng nhập: JWT của auth.js (mọi trang phải nạp auth.js TRƯỚC api.js).
var Api = (function () {
    // Giảm gọi trùng (database ở xa, mỗi API tốn hàng trăm ms):
    //  - GET cùng URL đang chạy dở thì dùng chung 1 request.
    //  - Vài API "tôi là ai" được nhớ ngắn hạn trong trang (SHORT_CACHE_MS).
    //  - Mọi request ghi (POST/PUT/PATCH/DELETE) xoá hết bộ nhớ này để lần đọc sau lấy dữ liệu mới.
    // Mỗi nơi gọi nhận 1 bản JSON riêng nên sửa object trả về không ảnh hưởng nơi khác.
    var SHORT_CACHE_MS = 10000;
    var SHORT_CACHE = [/^\/api\/teams\/mine$/, /^\/api\/teams\/my-teams$/,/^\/api\/tournaments\/\d+\/publish-status$/];
    var shared = {};   // url -> { promise, at, keep }

    function sharedGet(url) {
        var now = Date.now();
        var hit = shared[url];
        if (hit && (hit.pending || (hit.keep && now - hit.at < SHORT_CACHE_MS))) return hit.promise;
        var entry = { pending: true, at: now, keep: SHORT_CACHE.some(function (re) { return re.test(url); }) };
        entry.promise = send('GET', url).then(function (r) {
            entry.pending = false; entry.at = Date.now();
            if (!entry.keep && shared[url] === entry) delete shared[url];
            return r;
        }, function (e) {
            if (shared[url] === entry) delete shared[url];
            throw e;
        });
        shared[url] = entry;
        return entry.promise;
    }

    async function request(method, url, body, isForm) {
        var r;
        if (method === 'GET') {
            r = await sharedGet(url);
        } else {
            shared = {};
            r = await send(method, url, body, isForm);
        }
        var json = null;
        if (r.text) {
            try { json = JSON.parse(r.text); } catch (e) { json = null; }
        }
        if (!r.ok) {
            throw {
                status: r.status,
                message: (json && json.message) || ('Lỗi ' + r.status),
                fieldErrors: (json && (json.fieldErrors || (json.errors))) || {}
            };
        }
        return json || {};
    }

    async function send(method, url, body, isForm) {
        var opts = { method: method, credentials: 'same-origin', headers: {} };
        if (body !== undefined && body !== null) {
            if (isForm) {
                opts.body = body; // FormData — trình duyệt tự set Content-Type multipart boundary
            } else {
                opts.headers['Content-Type'] = 'application/json';
                opts.body = JSON.stringify(body);
            }
        }

        var res = await authedFetch(url, opts);
        return { ok: res.ok, status: res.status, text: await res.text() };
    }

    // fetch kèm JWT, tự làm mới token 1 lần khi 401. Trả Response thô (dùng chung cho JSON lẫn tệp).
    async function authedFetch(url, opts) {
        opts = opts || { method: 'GET', credentials: 'same-origin', headers: {} };
        var hasAuth = !!(window.Auth && Auth.getAccessToken);
        function attempt() {
            var headers = Object.assign({}, opts.headers);
            var token = hasAuth ? Auth.getAccessToken() : null;
            if (token) headers.Authorization = 'Bearer ' + token;
            return fetch(url, Object.assign({}, opts, { headers: headers }));
        }

        var res;
        try {
            res = await attempt();
            // Token hết hạn (hoặc tab mới chưa có token nhưng còn cookie "ghi nhớ đăng nhập"):
            // làm mới 1 lần rồi gửi lại. Làm mới thất bại = khách — trả nguyên 401 cho trang tự xử lý,
            // không đá sang trang đăng nhập (trang bắt buộc đăng nhập đã có Ui.requireAuth lo).
            if (res.status === 401 && hasAuth) {
                var refreshed = false;
                try { await Auth.refresh(); refreshed = true; } catch (e) { Auth.setAccessToken(null); }
                if (refreshed) res = await attempt();
            }
            if (res.status === 403 && hasAuth) {
                var banned = await res.clone().json().catch(function () { return {}; });
                if (banned.code === 'ACCOUNT_BANNED') {
                    try { sessionStorage.setItem('ban_info', JSON.stringify(banned)); } catch (e) {}
                    Auth.logout('/auth/login.html');
                }
            }
        } catch (e) {
            throw { status: 0, message: 'Không kết nối được máy chủ. Vui lòng thử lại.', fieldErrors: {} };
        }
        return res;
    }

    // Tải tệp riêng tư (biên lai, giấy tờ) kèm token: link <a href="/api/..."> thường không gửi token.
    async function openFile(url, fileName) {
        var res = await authedFetch(url);
        if (!res.ok) {
            var json = await res.json().catch(function () { return null; });
            throw { status: res.status, message: (json && json.message) || (res.status === 401
                ? 'Phiên đăng nhập đã hết, vui lòng đăng nhập lại.' : 'Không mở được tệp.'), fieldErrors: {} };
        }
        var blob = await res.blob();
        var objectUrl = URL.createObjectURL(blob);
        // Ảnh/PDF mở tab mới để xem; trình duyệt chặn cửa sổ bật lên thì tải về thay thế.
        var viewer = /^(image\/|application\/pdf)/.test(blob.type) ? window.open(objectUrl, '_blank') : null;
        if (!viewer) {
            var a = document.createElement('a');
            a.href = objectUrl;
            a.download = fileName || 'tep-dinh-kem';
            document.body.appendChild(a);
            a.click();
            a.remove();
        }
        setTimeout(function () { URL.revokeObjectURL(objectUrl); }, 60000);
    }

    return {
        openFile: openFile,
        clearCache: function () { shared = {}; },
        get: function (url) { return request('GET', url); },
        post: function (url, body) { return request('POST', url, body, false); },
        patch: function (url, body) { return request('PATCH', url, body, false); },
        postForm: function (url, formData) { return request('POST', url, formData, true); },
        put: function (url, body) { return request('PUT', url, body, false); },
        del: function (url, body) { return request('DELETE', url, body, false); }
    };
})();
