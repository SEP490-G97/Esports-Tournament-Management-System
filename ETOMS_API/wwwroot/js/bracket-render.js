// Vẽ cây nhánh loại trực tiếp bằng CSS Grid — port nguyên thuật toán từ
// Views/Bracket/_BracketTree.cshtml (bản Razor cũ, xem comment gốc trong file đó để hiểu
// "hàng mịn" fine-rows). Dùng chung cho bracket/view.html và bracket/manage.html.
//
// matches: mảng BracketMatchDto từ API (camelCase): roundNo, positionInRound, isBye,
//          teamAId/teamAName, teamBId/teamBName, winnerTeamId, matchId, isReadyToPick.
function renderBracketTree(container, matches) {
    var byRound = {};
    (matches || []).forEach(function (m) {
        (byRound[m.roundNo] = byRound[m.roundNo] || []).push(m);
    });
    var roundNos = Object.keys(byRound).map(Number).sort(function (a, b) { return a - b; });
    var rounds = roundNos.map(function (r) {
        return byRound[r].slice().sort(function (a, b) { return a.positionInRound - b.positionInRound; });
    });
    var roundCount = rounds.length;

    container.innerHTML = '';

    if (roundCount === 0) {
        container.innerHTML = '<div class="card-etoms"><div class="empty"><h3>Chưa có nhánh loại trực tiếp</h3></div></div>';
        return;
    }

    var round1Count = rounds[0].length;
    var unit = Math.pow(2, Math.max(roundCount - 1, 0));
    var fineRows = Math.max(round1Count * unit, 1);

    var colDefs = [];
    for (var i = 0; i < roundCount; i++) {
        colDefs.push('minmax(230px,1fr)');
        if (i < roundCount - 1) colDefs.push('52px');
    }

    var grid = document.createElement('div');
    grid.className = 'bracket-grid';
    grid.style.gridTemplateRows = 'repeat(' + fineRows + ', minmax(24px,1fr))';
    grid.style.gridTemplateColumns = colDefs.join(' ');

    for (var ri = 0; ri < roundCount; ri++) {
        var span = unit * Math.pow(2, ri);
        var gridCol = ri * 2 + 1;
        for (var k = 0; k < rounds[ri].length; k++) {
            var m = rounds[ri][k];
            var rowStart = k * span;
            var rowEnd = rowStart + span;

            var cell = document.createElement('div');
            cell.className = 'bracket-cell';
            cell.style.gridColumn = String(gridCol);
            cell.style.gridRow = (rowStart + 1) + ' / ' + (rowEnd + 1);

            var card = document.createElement('div');
            card.className = 'bracket-card';

            var bnA = document.createElement('div');
            bnA.className = 'bn' + (m.winnerTeamId != null && m.winnerTeamId === m.teamAId ? ' win' : '');
            bnA.textContent = m.teamAName || (m.isBye ? '—' : 'Chờ xác định');
            card.appendChild(bnA);

            if (!m.isBye) {
                var bnB = document.createElement('div');
                bnB.className = 'bn' + (m.winnerTeamId != null && m.winnerTeamId === m.teamBId ? ' win' : '');
                bnB.textContent = m.teamBName || 'Chờ xác định';
                card.appendChild(bnB);
            }

            if (m.isBye) {
                var tag = document.createElement('div');
                tag.className = 'bracket-tag';
                tag.textContent = 'Miễn đấu (Bye)';
                card.appendChild(tag);
            } else if (m.isReadyToPick && m.matchId != null) {
                var link = document.createElement('a');
                link.className = 'badge badge-soft';
                link.style.marginTop = '6px';
                link.style.textDecoration = 'none';
                link.href = '/result/evidence.html?matchId=' + m.matchId;
                link.textContent = 'Xem/Báo cáo kết quả →';
                card.appendChild(link);
            }

            cell.appendChild(card);
            grid.appendChild(cell);
        }
    }

    for (var ci = 0; ci < roundCount - 1; ci++) {
        var spanChild = unit * Math.pow(2, ci);
        var connectorCol = ci * 2 + 2;
        var parents = rounds[ci + 1];
        for (var j = 0; j < parents.length; j++) {
            var topCenter = (2 * j) * spanChild + Math.floor(spanChild / 2);
            var bottomCenter = (2 * j + 1) * spanChild + Math.floor(spanChild / 2);

            var conn = document.createElement('div');
            conn.className = 'bracket-connector';
            conn.style.gridColumn = String(connectorCol);
            conn.style.gridRow = (topCenter + 1) + ' / ' + (bottomCenter + 1);
            ['bc-top', 'bc-bottom', 'bc-vert', 'bc-out'].forEach(function (cls) {
                var s = document.createElement('span');
                s.className = cls;
                conn.appendChild(s);
            });
            grid.appendChild(conn);
        }
    }

    var wrap = document.createElement('div');
    wrap.className = 'bracket-wrap';
    wrap.appendChild(grid);
    container.appendChild(wrap);
}
