(function () {
    var MARKS = {
        QUALIFIED: ['badge-active', 'Đi tiếp'],
        PROVISIONAL: ['badge-active', 'Tạm đi tiếp'],
        PENDING_DECISION: ['badge-gold', 'Chờ BTC xếp hạng']
    };
    var REFRESH_MS = 60000;

    var tournamentId = null;
    var board = null;
    var selected = null;
    var timer = null;

    function showEmpty(title, message) {
        document.getElementById('standings-root').innerHTML =
            '<div class="card-etoms"><div class="empty"><h3>' + Ui.escapeHtml(title) + '</h3>' +
            '<p>' + Ui.escapeHtml(message) + '</p></div></div>';
    }

    function formatTime(value) {
        if (!value) return '';
        var date = new Date(/Z$/.test(value) ? value : value + 'Z');
        return date.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' });
    }

    // Vòng bảng chưa xong thì hòa chỉ là tạm thời, chưa phải "sau mọi tiêu chí".
    function tieText(r) {
        if (r.isTied) return board.state === 'IN_PROGRESS' ? 'Đang bằng nhau' : 'Bằng nhau sau mọi tiêu chí';
        return r.decidedBy || '';
    }

    function tableHtml(group) {
        var rows = group.rows.map(function (r) {
            var mark = MARKS[r.mark];
            var inZone = r.mark === 'QUALIFIED' || r.mark === 'PROVISIONAL';
            return '<tr class="' + (inZone ? 'in-zone' : '') + '" data-team="' + r.teamId + '">' +
                '<td>' + r.position + '</td>' +
                '<td class="team">' + Ui.escapeHtml(r.teamName) + (mark ? ' <span class="badge ' + mark[0] + '">' + mark[1] + '</span>' : '') + '</td>' +
                '<td>' + r.played + '</td>' +
                '<td>' + r.won + ' - ' + r.lost + '</td>' +
                '<td>' + r.gamesWon + '–' + r.gamesLost + '</td>' +
                '<td>' + (r.diff > 0 ? '+' : '') + r.diff + '</td>' +
                '<td><strong>' + r.points + '</strong></td>' +
                '<td class="tb">' + Ui.escapeHtml(tieText(r)) + '</td></tr>';
        }).join('');
        return '<div class="standing-wrap"><table class="standing" id="standing-table" data-group="' + Ui.escapeHtml(group.groupLabel) + '"><thead><tr>' +
            '<th>Hạng</th><th class="team">Đội tuyển</th><th title="Số trận đã có kết quả">MP</th><th title="Thắng - Thua">W - L</th>' +
            '<th>Ván</th><th>Hiệu số</th><th>Điểm</th><th class="tb">Tie-breaker đã áp dụng</th>' +
            '</tr></thead><tbody>' + rows + '</tbody></table></div>';
    }

    function resultsHtml(group) {
        if (!group.matches.length) return '';
        return '<div class="section-title" style="margin:22px 0 6px;font-size:1rem">Các trận của bảng ' + Ui.escapeHtml(group.groupLabel) + '</div>' +
            '<div id="result-list">' + group.matches.map(function (m) {
                var score = m.isDecided && m.scoreA !== null ? m.scoreA + ' – ' + m.scoreB : 'vs';
                var when = m.scheduledAt && !m.isDecided ? ' · ' + formatTime(m.scheduledAt) : '';
                return '<div class="result-row" data-result="' + m.matchId + '">' +
                    '<span class="rl" style="min-width:64px">Lượt ' + m.roundNo + '</span>' +
                    '<span class="grow" style="text-align:right">' + Ui.escapeHtml(m.teamAName) + '</span>' +
                    '<span class="score">' + score + '</span>' +
                    '<span class="grow">' + Ui.escapeHtml(m.teamBName) + '</span>' +
                    '<span class="badge ' + Ui.escapeHtml(m.statusClass) + '">' + Ui.escapeHtml(m.statusLabel) + Ui.escapeHtml(when) + '</span>' +
                    '</div>';
            }).join('') + '</div>';
    }

    function legendHtml(b) {
        if (!b.advancePerGroup) return '';
        return '<div class="hint" style="margin-top:12px">' + b.advancePerGroup + ' đội đầu mỗi bảng đi tiếp vào vòng knockout. ' +
            '"Tạm đi tiếp" là vị trí hiện tại khi vòng bảng chưa xong.</div>';
    }

    function render() {
        var b = board;
        document.getElementById('page-title').textContent = 'Bảng xếp hạng · ' + b.tournamentName;
        document.getElementById('page-sub').textContent = b.formatLabel + ' · Thắng 3 điểm, thua 0 điểm. Bằng điểm xét: ' + b.tieBreakerOrder.join(' > ') + '.';

        var head = '<div class="card-etoms"><div class="body">' +
            '<p style="margin:0" id="state-message">' + Ui.escapeHtml(b.stateMessage) + '</p>' +
            (b.isLocked || !b.groups.length ? '' : '<div class="pending-banner" style="margin-top:10px">Nhánh đấu chưa khóa, các bảng có thể còn thay đổi.</div>') +
            '<div class="hint" style="margin-top:8px" id="updated-at">Cập nhật lúc ' + new Date().toLocaleTimeString('vi-VN') + '</div>' +
            (b.groups.length ? '<div class="btn-row"><button type="button" class="btn btn-ghost" id="export-btn">Xuất báo cáo (CSV)</button></div>' : '') +
            '</div></div>';

        if (!b.groups.length) {
            document.getElementById('standings-root').innerHTML = head;
            return;
        }

        if (!selected || !b.groups.some(function (g) { return g.groupLabel === selected; })) selected = b.groups[0].groupLabel;
        var group = b.groups.find(function (g) { return g.groupLabel === selected; });

        var tabs = '<div class="group-tabs" role="tablist" aria-label="Chọn bảng">' + b.groups.map(function (g) {
            var on = g.groupLabel === selected;
            return '<button type="button" class="btn btn-ghost" role="tab" aria-selected="' + on + '" data-tab="' + Ui.escapeHtml(g.groupLabel) + '">Bảng ' + Ui.escapeHtml(g.groupLabel) + '</button>';
        }).join('') + '</div>';

        document.getElementById('standings-root').innerHTML = head + tabs +
            '<div class="card-etoms" style="margin-top:10px"><div class="body">' +
            '<div class="section-title" style="margin-top:0">Bảng ' + Ui.escapeHtml(group.groupLabel) + '</div>' +
            tableHtml(group) + legendHtml(b) + resultsHtml(group) +
            '</div></div>';

        document.querySelectorAll('[data-tab]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                selected = btn.dataset.tab;
                try { history.replaceState(null, '', '#' + encodeURIComponent(selected)); } catch (e) { /* bỏ qua */ }
                render();
            });
        });
        var exportBtn = document.getElementById('export-btn');
        if (exportBtn) exportBtn.addEventListener('click', exportCsv);
    }

    function csvCell(value) {
        var text = value === null || value === undefined ? '' : String(value);
        return /[",\r\n]/.test(text) ? '"' + text.replace(/"/g, '""') + '"' : text;
    }

    // Xuất mọi bảng ra CSV có BOM để Excel đọc đúng tiếng Việt.
    function exportCsv() {
        var marks = { QUALIFIED: 'Đi tiếp', PROVISIONAL: 'Tạm đi tiếp', PENDING_DECISION: 'Chờ BTC xếp hạng', OUT: 'Bị loại' };
        var lines = [['Bảng', 'Hạng', 'Đội tuyển', 'Số trận', 'Thắng', 'Thua', 'Ván thắng', 'Ván thua', 'Hiệu số', 'Điểm', 'Tie-breaker đã áp dụng', 'Suất đi tiếp']];
        board.groups.forEach(function (g) {
            g.rows.forEach(function (r) {
                lines.push([g.groupLabel, r.position, r.teamName, r.played, r.won, r.lost, r.gamesWon, r.gamesLost, r.diff, r.points, tieText(r), marks[r.mark] || '']);
            });
        });
        var csv = '﻿' + lines.map(function (l) { return l.map(csvCell).join(','); }).join('\r\n');
        var blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
        var link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = 'bang-xep-hang-giai-' + tournamentId + '.csv';
        document.body.appendChild(link);
        link.click();
        setTimeout(function () { URL.revokeObjectURL(link.href); link.remove(); }, 0);
    }

    async function load(first) {
        try {
            var res = await Api.get('/api/tournaments/' + tournamentId + '/standings');
            board = res.data;
            render();
        } catch (err) {
            if (!first) return; // lần làm mới lỗi thì giữ bảng cũ
            if (err.status === 404) showEmpty('Không tìm thấy giải đấu', 'Giải không tồn tại hoặc chưa được công bố.');
            else showEmpty('Không tải được bảng xếp hạng', err.message);
            return;
        }
        scheduleRefresh();
    }

    // Vòng bảng còn đấu thì tự làm mới mỗi phút, chỉ khi trang đang được xem.
    function scheduleRefresh() {
        clearTimeout(timer);
        if (!board || board.state !== 'IN_PROGRESS') return;
        timer = setTimeout(function () {
            if (document.visibilityState === 'visible') load(false);
            else scheduleRefresh();
        }, REFRESH_MS);
    }

    document.addEventListener('DOMContentLoaded', function () {
        tournamentId = Ui.qsInt('id');
        if (tournamentId === null) {
            showEmpty('Không xác định được giải đấu', 'Đường dẫn thiếu mã giải.');
            return;
        }
        if (location.hash.length > 1) {
            try { selected = decodeURIComponent(location.hash.slice(1)); } catch (e) { selected = null; }
        }
        load(true);
    });
})();
