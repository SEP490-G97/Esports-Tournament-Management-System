// Bọc fetch() theo đúng envelope JSON của ApiControllerBase.FromResult():
//  - Thành công: { message, data? }
//  - Thất bại:   { message, fieldErrors: { Field: "msg" } }   (lỗi ném ra có .status/.message/.fieldErrors)
// Cookie session (đăng nhập demo) tự gửi kèm vì trang tĩnh và API cùng origin.
var Api = (function () {
    async function request(method, url, body, isForm) {
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
            res = await fetch(url, opts);
        } catch (e) {
            throw { status: 0, message: 'Không kết nối được máy chủ. Vui lòng thử lại.', fieldErrors: {} };
        }

        var text = await res.text();
        var json = null;
        if (text) {
            try { json = JSON.parse(text); } catch (e) { json = null; }
        }

        if (!res.ok) {
            throw {
                status: res.status,
                message: (json && json.message) || ('Lỗi ' + res.status),
                fieldErrors: (json && (json.fieldErrors || (json.errors))) || {}
            };
        }
        return json || {};
    }

    return {
        get: function (url) { return request('GET', url); },
        post: function (url, body) { return request('POST', url, body, false); },
        patch: function (url, body) { return request('PATCH', url, body, false); },
        postForm: function (url, formData) { return request('POST', url, formData, true); },
        put: function (url, body) { return request('PUT', url, body, false); },
        del: function (url, body) { return request('DELETE', url, body, false); }
    };
})();

res = await Auth.apiFetch(url, opts);
