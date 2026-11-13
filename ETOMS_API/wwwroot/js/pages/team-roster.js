(function () {
    var teamId = null;

    function pendingItemHtml(p, locked) {
        var msg = p.message ? ' · “' + Ui.escapeHtml(p.message) + '”' : '';
        var actions = locked ? '' :
            '<div style="display:flex;gap:8px">' +
            '<button type="button" class="btn btn-primary btn-sm" data-act="approve" data-target="' + p.accountId + '">Duyệt</button>' +
            '<button type="button" class="btn btn-ghost btn-sm text-danger-etoms" data-act="reject" data-target="' + p.accountId + '" data-name="' + Ui.escapeHtml(p.fullName) + '">Từ chối</button>' +
            '</div>';
        return '<div class="card-etoms"><div class="body" style="display:flex;align-items:center;gap:16px;flex-wrap:wrap;padding:16px 20px;border-left:3px solid var(--warn);border-radius:0 14px 14px 0">' +
            '<span class="av" style="width:46px;height:46px;border-radius:11px;background:var(--panel-2);border:1px solid var(--border);display:grid;place-items:center;font-weight:800;color:var(--warn)">' + Ui.escapeHtml(p.fullName && p.fullName.length > 0 ? p.fullName.substring(0, 1) : '?') + '</span>' +
            '<div style="flex:1;min-width:160px"><div class="fw-bold">' + Ui.escapeHtml(p.fullName) + '</div><div class="text-small text-muted-etoms">' + Ui.escapeHtml(p.directionLabel) + msg + '</div></div>' +
            actions + '</div></div>';
    }

    function memberItemHtml(m, roster) {
        var badge = m.isCaptain ? '<span class="badge badge-gold">Đội trưởng</span>'
            : m.slot === 'STARTER' ? '<span class="badge badge-soft">Chính thức</span>'
            : '<span class="badge" style="background:var(--panel-2);color:var(--muted)">Dự bị</span>';

        var actions = '';
        if (!m.isCaptain && !roster.isLocked) {
            actions += '<div style="display:flex;gap:8px;flex-wrap:wrap">';
            if (m.slot === 'SUBSTITUTE' && roster.canAddStarter) {
                actions += '<button type="button" class="btn btn-ghost btn-sm" data-act="slot-starter" data-target="' + m.accountId + '">Lên chính thức</button>';
            }
            if (m.slot === 'STARTER' && roster.canAddSub) {
                actions += '<button type="button" class="btn btn-ghost btn-sm" data-act="slot-sub" data-target="' + m.accountId + '">Xuống dự bị</button>';
            }
            actions += '<button type="button" class="btn btn-ghost btn-sm text-gold" data-act="transfer" data-target="' + m.accountId + '" data-name="' + Ui.escapeHtml(m.fullName) + '">Đặt làm đội trưởng</button>';
            actions += '<button type="button" class="btn btn-ghost btn-sm text-danger-etoms" data-act="kick" data-target="' + m.accountId + '" data-name="' + Ui.escapeHtml(m.fullName) + '">Loại</button>';
            actions += '</div>';
        }

        return '<div class="card-etoms"><div class="body" style="display:flex;align-items:center;gap:16px;flex-wrap:wrap;padding:16px 20px">' +
            '<span class="av" style="width:46px;height:46px;border-radius:11px;background:var(--panel-2);border:1px solid var(--border);display:grid;place-items:center;font-weight:800;color:var(--cyan)">' + Ui.escapeHtml(m.fullName && m.fullName.length > 0 ? m.fullName.substring(0, 1) : '?') + '</span>' +
            '<div style="flex:1;min-width:160px"><div class="fw-bold">' + Ui.escapeHtml(m.fullName) + '</div><div class="text-small text-muted-etoms">' + badge + '</div></div>' +
            actions + '</div></div>';
    }

    function render(roster) {
        var root = document.getElementById('roster-root');

        var lockedAlert = roster.isLocked
            ? '<div class="alert" style="background:rgba(246,167,35,.1);border:1px solid rgba(246,167,35,.3);color:var(--warn)">🔒 Roster đang <strong>bị khóa</strong> vì đội đang thi đấu trong một giải. Mọi thay đổi thành viên tạm thời bị vô hiệu.</div>'
            : '';

        var pendingHtml = '';
        if (roster.pendingMembers.length) {
            pendingHtml = '<div class="section-title mt-0">Đơn chờ duyệt (' + roster.pendingMembers.length + ')</div>' +
                '<div class="section-sub">Ứng tuyển hoặc lời mời đang chờ đội trưởng xác nhận.</div>' +
                '<div class="d-grid gap-3 mb-4">' + roster.pendingMembers.map(function (p) { return pendingItemHtml(p, roster.isLocked); }).join('') + '</div>';
        }

        var membersHtml = '<div class="d-grid gap-3 mt-2">' + roster.members.map(function (m) { return memberItemHtml(m, roster); }).join('') + '</div>';

        root.innerHTML =
            '<div class="eyebrow">Quản trị đội · Roster</div>' +
            '<div class="page-head"><h1>Quản lý Roster — ' + Ui.escapeHtml(roster.name) + '</h1>' +
            '<p>Phân loại thành viên chính thức / dự bị theo luật ' + Ui.escapeHtml(roster.gameTitle) + ', loại thành viên hoặc chuyển quyền đội trưởng.</p></div>' +
            lockedAlert + pendingHtml +
            '<div class="section-title mt-0">Thành viên chính thức</div>' +
            '<div class="section-sub">Chính thức: <strong class="text-ink">' + roster.starterCount + '/' + roster.maxStarters + '</strong> · Dự bị: <strong class="text-ink">' + roster.subCount + '/' + roster.maxSubstitutes + '</strong></div>' +
            membersHtml +
            '<div class="btn-row mt-4"><a class="btn btn-ghost" href="/team/dashboard.html?id=' + teamId + '">← Về trang quản trị</a></div>';

        root.querySelectorAll('[data-act]').forEach(function (btn) {
            btn.addEventListener('click', function () { handleAction(btn.dataset.act, btn.dataset.target, btn.dataset.name); });
        });
    }

    async function load() {
        try {
            var roster = await Api.get('/api/teams/' + teamId + '/roster');
            render(roster);
        } catch (err) {
            var root = document.getElementById('roster-root');
            if (err.status === 403) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Bạn không phải đội trưởng đội này</h3></div></div>';
            else if (err.status === 404) root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Không tìm thấy đội</h3></div></div>';
            else root.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Có lỗi xảy ra</h3><p>' + Ui.escapeHtml(err.message) + '</p></div></div>';
        }
    }

    async function handleAction(act, targetAccountId, name) {
        var flash = document.getElementById('flash');
        try {
            var res;
            if (act === 'approve') {
                res = await Api.post('/api/teams/' + teamId + '/members/' + targetAccountId + '/approve');
            } else if (act === 'reject') {
                if (!(await window.etomsConfirm('Từ chối đơn của ' + name + '?'))) return;
                res = await Api.post('/api/teams/' + teamId + '/members/' + targetAccountId + '/reject');
            } else if (act === 'slot-starter') {
                res = await Api.post('/api/teams/' + teamId + '/members/' + targetAccountId + '/slot', { NewSlot: 'STARTER' });
            } else if (act === 'slot-sub') {
                res = await Api.post('/api/teams/' + teamId + '/members/' + targetAccountId + '/slot', { NewSlot: 'SUBSTITUTE' });
            } else if (act === 'transfer') {
                if (!(await window.etomsConfirm('Chuyển quyền đội trưởng cho ' + name + '?'))) return;
                res = await Api.post('/api/teams/' + teamId + '/captain', { NewCaptainAccountId: parseInt(targetAccountId, 10) });
            } else if (act === 'kick') {
                if (!(await window.etomsConfirm('Loại ' + name + ' khỏi đội?'))) return;
                res = await Api.del('/api/teams/' + teamId + '/members/' + targetAccountId);
            }
            Ui.showAlert(flash, 'success', res.message);
            await load();
        } catch (err) {
            Ui.showAlert(flash, 'danger', err.message);
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        teamId = Ui.qsInt('id');
        if (!teamId) { document.getElementById('roster-root').innerHTML = '<div class="card-etoms"><div class="empty"><h3>Thiếu mã đội</h3></div></div>'; return; }
        var me = await Ui.requireAuth();
        if (!me) return;
        await load();
    });
})();
