/* ETOMS – tiện ích dùng chung cho trang quản trị ADM-03 / ADM-06 / ADM-08.
   Phải nạp SAU auth.js / api.js (không cần Bootstrap). Mọi nội dung động được dựng bằng textContent (không innerHTML). */
(function () {
  'use strict';

  /** Gọi API quản trị qua Auth.apiFetch. Trả { ok, status, data }. body là object (JSON) hoặc FormData. */
  async function api(path, opts) {
    opts = opts || {};
    const init = { method: opts.method || 'GET', headers: {} };
    if (opts.body instanceof FormData) {
      init.body = opts.body; // trình duyệt tự đặt boundary
    } else if (opts.body !== undefined) {
      init.headers['Content-Type'] = 'application/json';
      init.body = JSON.stringify(opts.body);
    }
    let res;
    try {
      res = await Auth.apiFetch(path, init);
    } catch (e) {
      return { ok: false, status: 0, data: { code: 'NETWORK', message: 'Không kết nối được máy chủ.' } };
    }
    if (!res) return { ok: false, status: 0, data: { code: 'NETWORK', message: 'Không kết nối được máy chủ.' } };
    let data = null;
    if (res.status !== 204) {
      try { data = await res.json(); } catch (e) { data = null; }
    }
    return { ok: res.ok, status: res.status, data: data };
  }

  function errMsg(res, fallback) {
    if (res && res.data && res.data.message) return res.data.message;
    if (res && res.status === 429) return 'Thao tác quá nhanh, hãy thử lại sau.';
    if (res && res.status === 403) return 'Bạn không có quyền thực hiện thao tác này.';
    return fallback || 'Có lỗi xảy ra, vui lòng thử lại.';
  }

  /** el('div', {class:'x', text:'..', onclick: fn, attrs:{...}}, child1, child2) */
  function el(tag, props) {
    const node = document.createElement(tag);
    props = props || {};
    for (const k of Object.keys(props)) {
      const v = props[k];
      if (v === undefined || v === null || v === false) continue;
      if (k === 'class') node.className = v;
      else if (k === 'text') node.textContent = v;
      else if (k === 'attrs') for (const a of Object.keys(v)) node.setAttribute(a, v[a]);
      else if (k.startsWith('on') && typeof v === 'function') node.addEventListener(k.slice(2), v);
      else if (k in node) node[k] = v;
      else node.setAttribute(k, v);
    }
    for (let i = 2; i < arguments.length; i++) {
      const c = arguments[i];
      if (c === null || c === undefined || c === false) continue;
      node.appendChild(typeof c === 'string' || typeof c === 'number' ? document.createTextNode(String(c)) : c);
    }
    return node;
  }

  function clear(node) { while (node.firstChild) node.removeChild(node.firstChild); }

  /** Chuỗi thời gian từ API là UTC không có "Z": thêm "Z" trước khi parse. */
  function parseUtc(s) {
    if (!s) return null;
    const str = String(s);
    return new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(str) ? str : str + 'Z');
  }
  function fmtDateTime(s) {
    const d = parseUtc(s);
    return d && !isNaN(d) ? d.toLocaleString('vi-VN', { hour12: false }) : '—';
  }
  function fmtNumber(n) { return Number(n || 0).toLocaleString('vi-VN'); }

  /** UTC → giá trị cho <input type="datetime-local"> theo giờ máy. */
  function toLocalInput(s) {
    const d = parseUtc(s);
    if (!d || isNaN(d)) return '';
    const p = (x) => String(x).padStart(2, '0');
    return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) + 'T' + p(d.getHours()) + ':' + p(d.getMinutes());
  }
  /** Giá trị datetime-local → ISO UTC ("" → null). */
  function fromLocalInput(v) {
    if (!v) return null;
    const d = new Date(v);
    return isNaN(d) ? null : d.toISOString();
  }

  function debounce(fn, ms) {
    let t;
    return function () { const a = arguments, c = this; clearTimeout(t); t = setTimeout(function () { fn.apply(c, a); }, ms); };
  }

  function toast(message, kind) {
    let box = document.getElementById('ax-toasts');
    if (!box) { box = el('div', { id: 'ax-toasts', class: 'ax-toasts', attrs: { 'aria-live': 'polite' } }); document.body.appendChild(box); }
    const t = el('div', { class: 'ax-toast ' + (kind || 'success'), text: message, attrs: { role: kind === 'error' ? 'alert' : 'status' } });
    box.appendChild(t);
    setTimeout(function () { t.remove(); }, kind === 'error' ? 6000 : 3500);
  }

  /** Hộp xác nhận bằng thẻ <dialog>. Trả Promise<boolean>. */
  function confirmDialog(opts) {
    return new Promise(function (resolve) {
      let decided = false;
      const title = el('h2', { class: 'ax-dialog-title', text: opts.title || 'Xác nhận' });
      const body = el('div', { class: 'ax-dialog-body' });
      (Array.isArray(opts.message) ? opts.message : [opts.message || '']).forEach(function (line) {
        body.appendChild(el('p', { text: line, class: 'ax-p' }));
      });
      const cancel = el('button', { type: 'button', class: 'ax-btn ghost', text: opts.cancelText || 'Hủy' });
      const ok = el('button', { type: 'button', class: 'ax-btn' + (opts.danger ? ' danger' : ''), text: opts.okText || 'Đồng ý' });
      const dialog = el('dialog', { class: 'ax-dialog' },
        el('div', { class: 'ax-dialog-head' }, title),
        body,
        el('div', { class: 'ax-dialog-foot' }, cancel, ok));
      document.body.appendChild(dialog);
      function done(v) { if (decided) return; decided = true; resolve(v); if (dialog.open) dialog.close(); }
      cancel.addEventListener('click', function () { done(false); });
      ok.addEventListener('click', function () { done(true); });
      dialog.addEventListener('click', function (e) { if (e.target === dialog) done(false); });
      dialog.addEventListener('close', function () { if (!decided) { decided = true; resolve(false); } dialog.remove(); });
      dialog.showModal();
      cancel.focus();
    });
  }

  /** Thanh điều hướng giữa các trang admin; đặt <nav id="admin-nav"></nav> trong trang. */
  function renderNav(active) {
    const nav = document.getElementById('admin-nav');
    if (!nav) return;
    clear(nav);
    nav.setAttribute('aria-label', 'Khu vực quản trị');
    [
      ['user', '/admin/user.html', 'Tài khoản'],
      ['games', '/admin/games.html', 'Danh mục Game']
      // ADM-06 (dashboard) và ADM-08 (announcements) sẽ thêm vào đây khi hoàn thành
    ].forEach(function (x) {
      const a = el('a', { href: x[1], text: x[2] });
      if (x[0] === active) { a.className = 'active'; a.setAttribute('aria-current', 'page'); }
      nav.appendChild(a);
    });
  }

  /** Khóa/mở nút khi đang gọi API. */
  function busy(btn, on, label) {
    if (!btn) return;
    if (on) { btn.dataset.label = btn.textContent; btn.disabled = true; if (label) btn.textContent = label; }
    else { btn.disabled = false; if (btn.dataset.label) btn.textContent = btn.dataset.label; }
  }

  window.AdminUI = {
    api: api, errMsg: errMsg, el: el, clear: clear, parseUtc: parseUtc, fmtDateTime: fmtDateTime,
    fmtNumber: fmtNumber, toLocalInput: toLocalInput, fromLocalInput: fromLocalInput, debounce: debounce,
    toast: toast, confirm: confirmDialog, renderNav: renderNav, busy: busy
  };
})();
