// Bọc fetch() theo đúng envelope JSON của ApiControllerBase.FromResult():
//  - Thành công: { message, data? }
//  - Thất bại:   { message, fieldErrors: { Field: "msg" } }   (lỗi ném ra có .status/.message/.fieldErrors)
// Cookie session (đăng nhập demo) tự gửi kèm vì trang tĩnh và API cùng origin.
var Api = (function () {
    // Giảm gọi trùng (database ở xa, mỗi API tốn hàng trăm ms):
    //  - GET cùng URL đang chạy dở thì dùng chung 1 request (vd layout.js và trang cùng hỏi /api/dev-auth/me).
    //  - Vài API "tôi là ai" được nhớ ngắn hạn trong trang (SHORT_CACHE_MS).
    //  - Mọi request ghi (POST/PUT/PATCH/DELETE) xoá hết bộ nhớ này để lần đọc sau lấy dữ liệu mới.
    // Mỗi nơi gọi nhận 1 bản JSON riêng nên sửa object trả về không ảnh hưởng nơi khác.
    var SHORT_CACHE_MS = 10000;
    var SHORT_CACHE = [/^\/api\/dev-auth\/me$/, /^\/api\/teams\/mine$/, /^\/api\/teams\/my-teams$/,/^\/api\/tournaments\/\d+\/publish-status$/];
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

        var res;
        try {
            // Đã đăng nhập thật (JWT, AUTH-02) thì đi qua Auth.apiFetch để gửi kèm token và tự refresh;
            // còn lại (khách, đăng nhập thử /dev-auth) gọi fetch như cũ.
            res = (window.Auth && Auth.getAccessToken && Auth.getAccessToken())
                ? await Auth.apiFetch(url, opts)
                : await fetch(url, opts);
        } catch (e) {
            throw { status: 0, message: 'Không kết nối được máy chủ. Vui lòng thử lại.', fieldErrors: {} };
        }

        return { ok: res.ok, status: res.status, text: await res.text() };
    }

    return {
        clearCache: function () { shared = {}; },
        get: function (url) { return request('GET', url); },
        post: function (url, body) { return request('POST', url, body, false); },
        patch: function (url, body) { return request('PATCH', url, body, false); },
        postForm: function (url, formData) { return request('POST', url, formData, true); },
        put: function (url, body) { return request('PUT', url, body, false); },
        del: function (url, body) { return request('DELETE', url, body, false); }
    };
})();
