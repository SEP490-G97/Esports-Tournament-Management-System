(function () {
    var query = new URLSearchParams(location.search);
    var tournamentId = Number(query.get('tournamentId'));
    var affiliateCode = (query.get('ref') || '').trim().toUpperCase();
    var flash = document.getElementById('landing-flash');

    function formatDate(value) {
        return value ? new Date(value).toLocaleString('vi-VN') : '—';
    }

    function safeExternalUrl(value) {
        if (!value) return null;
        try {
            var url = new URL(value, location.origin);
            return url.protocol === 'https:' || url.protocol === 'http:' ? url.href : null;
        } catch (_) {
            return null;
        }
    }

    function setMeta(name, value, property) {
        var selector = property ? 'meta[property="' + name + '"]' : 'meta[name="' + name + '"]';
        var meta = document.querySelector(selector);
        if (!meta) {
            meta = document.createElement('meta');
            meta.setAttribute(property ? 'property' : 'name', name);
            document.head.appendChild(meta);
        }
        meta.content = value;
    }

    function addDetail(root, label, value) {
        var term = document.createElement('dt');
        term.className = 'col-sm-4';
        term.textContent = label;
        var description = document.createElement('dd');
        description.className = 'col-sm-8';
        description.textContent = value || 'Chưa công bố';
        root.append(term, description);
    }

    function render(data) {
        var name = data.name;
        document.title = name + ' · ETOMS';
        setMeta('description', data.description || ('Thông tin và đăng ký giải ' + name + ' trên ETOMS.'));
        setMeta('og:title', name + ' · ETOMS', true);
        setMeta('og:description', data.description || ('Đăng ký giải ' + name + ' trên ETOMS.'), true);
        var bannerUrl = safeExternalUrl(data.bannerUrl);
        if (bannerUrl) {
            var banner = document.getElementById('landing-banner');
            banner.src = bannerUrl;
            banner.alt = 'Ảnh giới thiệu giải ' + name;
            banner.hidden = false;
            setMeta('og:image', bannerUrl, true);
        }
        document.getElementById('landing-game').textContent = data.gameName + ' · ' + data.gameShortCode;
        document.getElementById('landing-name').textContent = name;
        document.getElementById('landing-description').textContent = data.description || 'Giải đấu đang cập nhật thông tin giới thiệu.';
        document.getElementById('landing-date').textContent = formatDate(data.startAt);
        document.getElementById('landing-capacity').textContent = data.registeredTeams + ' / ' + data.maxTeams + ' đội';
        document.getElementById('landing-fee').textContent = data.entryFee > 0
            ? new Intl.NumberFormat('vi-VN').format(data.entryFee) + ' ₫'
            : 'Miễn phí';
        document.getElementById('landing-prize').textContent = data.prizeStructure || 'Ban tổ chức chưa công bố giải thưởng.';
        var details = document.getElementById('landing-details');
        addDetail(details, 'Đơn vị tổ chức', data.organizingUnit);
        addDetail(details, 'Khu vực', data.region);
        addDetail(details, 'Thể thức', data.format);
        addDetail(details, 'Địa điểm', data.isOnline ? 'Thi đấu trực tuyến' : (data.venue || 'Chưa công bố'));
        addDetail(details, 'Hạn đăng ký', formatDate(data.regCloseAt));
        addDetail(details, 'Trạng thái', data.status);
        var sponsorRoot = document.getElementById('landing-sponsors');
        if (!data.sponsors.length) {
            sponsorRoot.textContent = 'Chưa có thông tin nhà tài trợ.';
        } else {
            sponsorRoot.replaceChildren();
            data.sponsors.forEach(function (sponsor) {
                var item = document.createElement('div');
                item.className = 'd-flex align-items-center gap-2 mb-2';
                var link = safeExternalUrl(sponsor.websiteUrl);
                var label = document.createElement(link ? 'a' : 'span');
                label.textContent = sponsor.name + ' · ' + sponsor.tier;
                if (link) {
                    label.href = link;
                    label.target = '_blank';
                    label.rel = 'noopener noreferrer';
                }
                var logoUrl = safeExternalUrl(sponsor.logoUrl);
                if (logoUrl) {
                    var image = document.createElement('img');
                    image.src = logoUrl;
                    image.alt = '';
                    image.width = 48;
                    image.height = 48;
                    item.append(image);
                }
                item.append(label);
                sponsorRoot.append(item);
            });
        }
        var refQuery = affiliateCode ? '&ref=' + encodeURIComponent(affiliateCode) : '';
        var register = document.getElementById('landing-register');
        if (data.registrationOpen) {
            register.href = '/registration/register.html?tournamentId=' + data.tournamentId + refQuery;
            register.textContent = 'Đăng ký giải';
        } else {
            register.removeAttribute('href');
            register.textContent = 'Đăng ký chưa mở';
        }
        register.setAttribute('aria-disabled', data.registrationOpen ? 'false' : 'true');
        if (!data.registrationOpen) {
            register.classList.remove('btn-primary');
            register.classList.add('btn-ghost');
        }
        document.getElementById('landing-qr').href =
            '/api/marketing/tournaments/' + data.tournamentId + '/qr.svg' +
            (affiliateCode ? '?code=' + encodeURIComponent(affiliateCode) : '');
        document.getElementById('landing-share').addEventListener('click', async function () {
            var shareUrl = new URL('/share/tournaments/' + data.tournamentId +
                (affiliateCode ? '?ref=' + encodeURIComponent(affiliateCode) : ''), location.origin).href;
            var shareData = { title: name, text: data.description || name, url: shareUrl };
            try {
                if (navigator.share) await navigator.share(shareData);
                else {
                    await navigator.clipboard.writeText(shareUrl);
                    Ui.showAlert(flash, 'success', 'Đã sao chép liên kết giải đấu.');
                }
            } catch (error) {
                if (error && error.name !== 'AbortError') {
                    Ui.showAlert(flash, 'danger', 'Không thể chia sẻ liên kết trên thiết bị này.');
                }
            }
        }, { once: true });
        document.getElementById('landing-content').hidden = false;
    }

    document.addEventListener('DOMContentLoaded', async function () {
        if (!Number.isInteger(tournamentId) || tournamentId <= 0) {
            Ui.showAlert(flash, 'danger', 'Liên kết thiếu mã giải đấu hợp lệ.');
            return;
        }
        try {
            if (affiliateCode) {
                localStorage.setItem('etoms-affiliate-code:' + tournamentId, affiliateCode);
                await Api.post('/api/marketing/affiliate-links/' + encodeURIComponent(affiliateCode) + '/visit');
            }
            var response = await Api.get('/api/marketing/tournaments/' + tournamentId);
            render(response.data);
        } catch (error) {
            Ui.showAlert(flash, 'danger', (error && error.message) || 'Không thể tải thông tin giải đấu.');
        }
    });
})();
