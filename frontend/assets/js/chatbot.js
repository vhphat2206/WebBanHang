// ADLV Chatbot — rule-based, fetch /api/products để tra cứu SP
(function () {
    const API_HOST = window.location.origin;
    const STORAGE_KEY = 'adlv_chat_history';
    const POS_KEY = 'adlv_chat_btn_pos';

    let isOpen = false;
    let productsCache = null;
    let productsCacheTime = 0;

    async function getProducts() {
        if (productsCache && Date.now() - productsCacheTime < 5 * 60 * 1000) return productsCache;
        try {
            const r = await fetch(API_HOST + '/api/products');
            productsCache = await r.json();
            productsCacheTime = Date.now();
            return productsCache;
        } catch { return []; }
    }

    function fmt(n) { return Math.round(n).toLocaleString('vi-VN') + '₫'; }

    function normalize(s) {
        return s.toLowerCase()
            .normalize('NFD').replace(/[̀-ͯ]/g, '')
            .replace(/đ/g, 'd').replace(/Đ/g, 'd');
    }

    function injectStyles() {
        const css = `
        #adlv-chat-btn { position: fixed; bottom: 24px; right: 24px; z-index: 9998; width: 60px; height: 60px; border-radius: 50%; background: linear-gradient(135deg, #DC2626, #991B1B); color: white; border: none; cursor: grab; box-shadow: 0 10px 30px rgba(220,38,38,.4); display: flex; align-items: center; justify-content: center; transition: transform .2s; user-select: none; touch-action: none; -webkit-user-select: none; }
        #adlv-chat-btn:active { cursor: grabbing; }
        #adlv-chat-btn:hover { transform: scale(1.08); }
        #adlv-chat-btn .pulse { position: absolute; inset: 0; border-radius: 50%; background: #DC2626; animation: adlv-pulse 2s infinite; opacity: .4; z-index: -1; }
        @keyframes adlv-pulse { 0%{transform:scale(1);opacity:.5} 100%{transform:scale(1.5);opacity:0} }
        #adlv-chat-panel { position: fixed; bottom: 100px; right: 24px; z-index: 9999; width: 380px; max-width: calc(100vw - 32px); height: 540px; max-height: calc(100vh - 140px); background: white; border-radius: 16px; box-shadow: 0 20px 60px rgba(0,0,0,.25); display: none; flex-direction: column; overflow: hidden; font-family: 'Inter', sans-serif; }
        #adlv-chat-panel.open { display: flex; animation: adlv-slide .25s ease-out; }
        @keyframes adlv-slide { from{transform:translateY(20px);opacity:0} to{transform:translateY(0);opacity:1} }
        .adlv-chat-header { background: linear-gradient(135deg, #DC2626, #991B1B); color: white; padding: 16px; display: flex; align-items: center; gap: 12px; }
        .adlv-chat-header .avatar { width: 40px; height: 40px; border-radius: 50%; background: white; color: #DC2626; display: flex; align-items: center; justify-content: center; font-weight: 900; font-size: 14px; }
        .adlv-chat-header .info { flex: 1; }
        .adlv-chat-header .name { font-weight: 800; font-size: 14px; }
        .adlv-chat-header .status { font-size: 11px; opacity: .85; display: flex; align-items: center; gap: 4px; }
        .adlv-chat-header .status::before { content: ''; width: 6px; height: 6px; border-radius: 50%; background: #4ADE80; }
        .adlv-chat-header .close { background: rgba(255,255,255,.2); border: none; color: white; width: 28px; height: 28px; border-radius: 50%; cursor: pointer; font-size: 18px; line-height: 1; }
        .adlv-chat-body { flex: 1; overflow-y: auto; padding: 16px; background: #F9FAFB; display: flex; flex-direction: column; gap: 10px; }
        .adlv-msg { max-width: 80%; padding: 10px 14px; border-radius: 16px; font-size: 13px; line-height: 1.5; word-wrap: break-word; }
        .adlv-msg.bot { background: white; border: 1px solid #E5E7EB; align-self: flex-start; border-bottom-left-radius: 4px; }
        .adlv-msg.user { background: #DC2626; color: white; align-self: flex-end; border-bottom-right-radius: 4px; }
        .adlv-msg img { max-width: 100%; border-radius: 8px; margin-top: 6px; }
        .adlv-msg a { color: #DC2626; font-weight: 600; text-decoration: underline; }
        .adlv-msg.user a { color: white; }
        .adlv-product-card { display: flex; gap: 10px; padding: 8px; background: #F3F4F6; border-radius: 8px; margin-top: 6px; align-items: center; }
        .adlv-product-card img { width: 50px; height: 50px; object-fit: cover; border-radius: 6px; margin: 0; }
        .adlv-product-card .info { flex: 1; min-width: 0; }
        .adlv-product-card .name { font-size: 12px; font-weight: 600; color: #111; }
        .adlv-product-card .price { font-size: 12px; font-weight: 700; color: #DC2626; }
        .adlv-quick { display: flex; flex-wrap: wrap; gap: 6px; padding: 8px 16px 0; }
        .adlv-quick button { background: white; border: 1px solid #DC2626; color: #DC2626; padding: 6px 12px; border-radius: 16px; font-size: 12px; font-weight: 600; cursor: pointer; transition: .15s; }
        .adlv-quick button:hover { background: #DC2626; color: white; }
        .adlv-chat-input { padding: 12px 16px; border-top: 1px solid #E5E7EB; background: white; display: flex; gap: 8px; }
        .adlv-chat-input input { flex: 1; border: 1px solid #E5E7EB; border-radius: 24px; padding: 10px 16px; font-size: 13px; outline: none; }
        .adlv-chat-input input:focus { border-color: #DC2626; }
        .adlv-chat-input button { background: #DC2626; color: white; border: none; border-radius: 50%; width: 38px; height: 38px; cursor: pointer; display: flex; align-items: center; justify-content: center; }
        .adlv-chat-input button:hover { background: #991B1B; }
        .adlv-typing { display: flex; gap: 4px; padding: 12px 16px; background: white; border: 1px solid #E5E7EB; border-radius: 16px; border-bottom-left-radius: 4px; align-self: flex-start; }
        .adlv-typing span { width: 6px; height: 6px; background: #9CA3AF; border-radius: 50%; animation: adlv-bounce 1.4s infinite; }
        .adlv-typing span:nth-child(2) { animation-delay: .2s; }
        .adlv-typing span:nth-child(3) { animation-delay: .4s; }
        @keyframes adlv-bounce { 0%,60%,100%{transform:translateY(0)} 30%{transform:translateY(-6px)} }
        `;
        const style = document.createElement('style');
        style.textContent = css;
        document.head.appendChild(style);
    }

    function injectHTML() {
        const wrapper = document.createElement('div');
        wrapper.innerHTML = `
            <button id="adlv-chat-btn" aria-label="Mở chat ADLV">
                <span class="pulse"></span>
                <svg width="26" height="26" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"/></svg>
            </button>
            <div id="adlv-chat-panel">
                <div class="adlv-chat-header">
                    <div class="avatar">ADLV</div>
                    <div class="info">
                        <div class="name">Trợ lý ADLV</div>
                        <div class="status">Online — luôn sẵn sàng</div>
                    </div>
                    <button class="close" aria-label="Đóng">×</button>
                </div>
                <div class="adlv-chat-body" id="adlv-chat-body"></div>
                <div class="adlv-quick" id="adlv-chat-quick"></div>
                <form class="adlv-chat-input" id="adlv-chat-form">
                    <input type="text" id="adlv-chat-input" placeholder="Nhập tin nhắn..." autocomplete="off"/>
                    <button type="submit" aria-label="Gửi">
                        <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2.5" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M14 5l7 7m0 0l-7 7m7-7H3"/></svg>
                    </button>
                </form>
            </div>
        `;
        document.body.appendChild(wrapper);
    }

    function saveHistory(messages) {
        try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(messages.slice(-20))); } catch {}
    }
    function loadHistory() {
        try { return JSON.parse(sessionStorage.getItem(STORAGE_KEY) || '[]'); } catch { return []; }
    }

    function appendMsg(from, html) {
        const body = document.getElementById('adlv-chat-body');
        const div = document.createElement('div');
        div.className = 'adlv-msg ' + from;
        div.innerHTML = html;
        body.appendChild(div);
        body.scrollTop = body.scrollHeight;
        const history = loadHistory();
        history.push({ from, html });
        saveHistory(history);
    }

    function showTyping() {
        const body = document.getElementById('adlv-chat-body');
        const t = document.createElement('div');
        t.className = 'adlv-typing';
        t.id = 'adlv-typing-indicator';
        t.innerHTML = '<span></span><span></span><span></span>';
        body.appendChild(t);
        body.scrollTop = body.scrollHeight;
    }
    function hideTyping() {
        const t = document.getElementById('adlv-typing-indicator');
        if (t) t.remove();
    }

    function renderQuickReplies(replies) {
        const q = document.getElementById('adlv-chat-quick');
        q.innerHTML = '';
        replies.forEach(r => {
            const btn = document.createElement('button');
            btn.textContent = r.label;
            btn.onclick = () => handleUserMessage(r.value || r.label);
            q.appendChild(btn);
        });
    }

    function productCard(p) {
        const img = p.imageUrl || '';
        const price = p.salePrice
            ? `<span class="price">${fmt(p.salePrice)}</span> <span style="font-size:11px;color:#9CA3AF;text-decoration:line-through">${fmt(p.price)}</span>`
            : `<span class="price">${fmt(p.price)}</span>`;
        return `<a href="product.html?id=${p.id}" style="text-decoration:none">
            <div class="adlv-product-card">
                <img src="${img}" alt=""/>
                <div class="info"><div class="name">${p.name}</div><div>${price}</div></div>
            </div></a>`;
    }

    async function findProducts(query) {
        const all = await getProducts();
        const q = normalize(query);
        return all.filter(p => normalize(p.name).includes(q) || normalize(p.brand || '').includes(q));
    }

    const RESPONSES = {
        greeting: () => `Chào bạn! 👋 Mình là trợ lý ảo của ADLV. Mình có thể giúp gì cho bạn? Bạn có thể hỏi về sản phẩm, giá cả, voucher, ship hàng, v.v.`,
        thanks: () => `Cảm ơn bạn đã quan tâm ADLV ❤️ Có gì cần hỏi thêm cứ nhắn nhé!`,
        bye: () => `Hẹn gặp lại bạn! Chúc bạn shopping vui vẻ 🛍️`,

        shipping: () => `🚚 <b>Thông tin giao hàng:</b><br>
• Nội thành TP.HCM: 1-2 ngày, phí 25.000₫<br>
• Tỉnh khác: 3-5 ngày, phí 35.000₫<br>
• <b>Miễn phí ship</b> cho đơn ≥ 1.500.000₫<br>
• Kiểm tra hàng trước khi thanh toán (COD)`,

        returns: () => `🔄 <b>Chính sách đổi/trả:</b><br>
• Đổi size/màu trong <b>7 ngày</b> kể từ ngày nhận<br>
• Sản phẩm còn nguyên tag, chưa giặt, chưa qua sử dụng<br>
• Lỗi từ nhà sản xuất → đổi mới hoặc hoàn 100%<br>
• Liên hệ hotline 1900-ADLV để được hỗ trợ`,

        payment: () => `💳 <b>3 cách thanh toán:</b><br>
1. <b>COD</b> — Thu hộ khi nhận hàng<br>
2. <b>Thẻ tín dụng/ghi nợ</b> — Visa, Mastercard, JCB<br>
3. <b>Chuyển khoản</b> — Quét VietQR auto tạo<br><br>
Bạn chọn phương thức ở trang Thanh toán.`,

        voucher: () => `🎁 <b>Voucher tự động cấp khi mua:</b><br>
• Đơn ≥ <b>2.000.000₫</b> → giảm <b>200.000₫</b> cho đơn sau<br>
• Đơn ≥ <b>3.000.000₫</b> → giảm <b>400.000₫</b><br>
• Đơn ≥ <b>5.000.000₫</b> → <b>Tặng 1 sản phẩm bất kỳ</b> (miễn phí SP có giá cao nhất)<br><br>
Xem voucher của bạn tại trang Giỏ hàng.`,

        sizing: () => `📏 <b>Bảng size áo ADLV:</b><br>
• S — ngực 100cm, dài 68cm (45-55kg)<br>
• M — ngực 106cm, dài 70cm (55-65kg)<br>
• L — ngực 112cm, dài 72cm (65-75kg)<br>
• XL — ngực 118cm, dài 74cm (75-85kg)<br><br>
Áo ADLV form <b>oversized unisex</b>, có thể chọn nhỏ hơn 1 size nếu muốn ôm dáng.`,

        gift: () => `🎀 <b>Khuyến mãi đặc biệt — Mua 2 áo tặng 1 gấu:</b><br>
Mua từ <b>2 áo</b> trở lên trong cùng đơn hàng → được tặng <b>1 gấu bông ADLV</b> (chọn bản Nam hoặc Nữ).<br><br>
Gấu sẽ giao kèm đơn hàng, không cần nhập mã.`,

        contact: () => `📞 <b>Liên hệ ADLV Store:</b><br>
• Hotline: 1900-ADLV (24/7)<br>
• Email: support@adlvstore.vn<br>
• Zalo: 0901-234-567<br>
• Địa chỉ: 123 Nguyễn Huệ, Q.1, TP.HCM<br>
• Giờ mở cửa: 9:00 - 22:00 hằng ngày`,

        bestseller: async () => {
            const all = await getProducts();
            const best = all.filter(p => p.isBestseller).slice(0, 3);
            if (!best.length) return `Hiện chưa có sản phẩm bán chạy được đánh dấu.`;
            return `🔥 <b>Sản phẩm bán chạy nhất:</b>` + best.map(productCard).join('');
        },

        sale: async () => {
            const all = await getProducts();
            const sale = all.filter(p => p.salePrice && p.salePrice < p.price).slice(0, 3);
            if (!sale.length) return `Hiện không có sản phẩm nào đang sale. Theo dõi web để không bỏ lỡ đợt sale tiếp theo nhé!`;
            return `💸 <b>Đang sale:</b>` + sale.map(productCard).join('');
        },

        newProducts: async () => {
            const all = await getProducts();
            const news = all.filter(p => p.isNew).slice(0, 3);
            if (!news.length) return `Chưa có sản phẩm mới về.`;
            return `✨ <b>Sản phẩm mới về:</b>` + news.map(productCard).join('');
        },

        allProducts: async () => {
            const all = await getProducts();
            return `🛍️ Hiện shop có <b>${all.length} sản phẩm</b>. Xem đầy đủ tại <a href="category.html?type=all">Bộ sưu tập</a>.`;
        },

        notFound: () => `Mình chưa hiểu ý bạn lắm 😅 Bạn có thể hỏi mình về:<br>
• Tên sản phẩm cụ thể (vd "Fuzzy Rabbit")<br>
• Giá, size, ship, đổi trả<br>
• Voucher, khuyến mãi<br>
Hoặc chọn nhanh các nút gợi ý bên dưới ⬇️`
    };

    async function classify(text) {
        const n = normalize(text);

        if (/^(hi|hello|chao|xin chao|alo|hey)/i.test(n)) return RESPONSES.greeting();
        if (/(cam on|thanks|thank you|tks)/i.test(n)) return RESPONSES.thanks();
        if (/(tam biet|bye|goodbye|see you)/i.test(n)) return RESPONSES.bye();

        if (/(ship|giao hang|van chuyen|phi ship|mien phi ship)/i.test(n)) return RESPONSES.shipping();
        if (/(doi tra|hoan tien|tra hang|doi size|doi mau|return)/i.test(n)) return RESPONSES.returns();
        if (/(thanh toan|payment|cod|chuyen khoan|the tin dung|vietqr|qr)/i.test(n)) return RESPONSES.payment();
        if (/(voucher|ma giam gia|khuyen mai|uu dai|giam gia)/i.test(n) && !/sale|dang sale/i.test(n)) return RESPONSES.voucher();
        if (/(size|kich thuoc|bao nhieu kg|do nguoi|nang bao nhieu)/i.test(n)) return RESPONSES.sizing();
        if (/(tang gau|gau bong|mua 2 ao|qua tang)/i.test(n)) return RESPONSES.gift();
        if (/(lien he|hotline|sdt|so dien thoai|email|dia chi|gio mo cua|cua hang)/i.test(n)) return RESPONSES.contact();

        if (/(ban chay|bestseller|hot|noi tieng)/i.test(n)) return await RESPONSES.bestseller();
        if (/(sale|giam gia)/i.test(n)) return await RESPONSES.sale();
        if (/(moi ve|new|moi nhat|hang moi)/i.test(n)) return await RESPONSES.newProducts();
        if (/(bao nhieu sp|co bao nhieu|tat ca sp|catalog|bo suu tap)/i.test(n)) return await RESPONSES.allProducts();

        // Search by product name keyword
        if (n.length >= 3) {
            const matches = await findProducts(text);
            if (matches.length) {
                const show = matches.slice(0, 3);
                let html = `Mình tìm thấy <b>${matches.length} sản phẩm</b> phù hợp:`;
                html += show.map(productCard).join('');
                if (matches.length > 3) html += `<br><a href="category.html?type=all">Xem tất cả →</a>`;
                return html;
            }
        }

        return RESPONSES.notFound();
    }

    async function handleUserMessage(text) {
        if (!text || !text.trim()) return;
        const clean = text.trim();
        appendMsg('user', escapeHtml(clean));
        const input = document.getElementById('adlv-chat-input');
        if (input) input.value = '';

        showTyping();
        const delay = 400 + Math.random() * 400;
        const reply = await classify(clean);
        setTimeout(() => {
            hideTyping();
            appendMsg('bot', reply);
        }, delay);
    }

    function escapeHtml(s) {
        return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function openChat() {
        const panel = document.getElementById('adlv-chat-panel');
        panel.classList.add('open');
        isOpen = true;
        document.getElementById('adlv-chat-input').focus();

        const body = document.getElementById('adlv-chat-body');
        if (!body.children.length) {
            const history = loadHistory();
            if (history.length) {
                history.forEach(m => {
                    const d = document.createElement('div');
                    d.className = 'adlv-msg ' + m.from;
                    d.innerHTML = m.html;
                    body.appendChild(d);
                });
                body.scrollTop = body.scrollHeight;
            } else {
                appendMsg('bot', RESPONSES.greeting());
            }
        }
        renderQuickReplies([
            { label: '🛍️ Bán chạy', value: 'sản phẩm bán chạy' },
            { label: '💸 Đang sale', value: 'sản phẩm đang sale' },
            { label: '🎁 Voucher', value: 'voucher có gì' },
            { label: '🚚 Ship', value: 'phí ship' },
            { label: '🔄 Đổi trả', value: 'chính sách đổi trả' }
        ]);
    }
    function closeChat() {
        document.getElementById('adlv-chat-panel').classList.remove('open');
        isOpen = false;
    }

    function clampPos(x, y) {
        const btnSize = 60;
        const maxX = window.innerWidth - btnSize - 8;
        const maxY = window.innerHeight - btnSize - 8;
        return { x: Math.max(8, Math.min(x, maxX)), y: Math.max(8, Math.min(y, maxY)) };
    }

    function applyBtnPos(x, y) {
        const btn = document.getElementById('adlv-chat-btn');
        const panel = document.getElementById('adlv-chat-panel');
        const { x: cx, y: cy } = clampPos(x, y);
        btn.style.left = cx + 'px';
        btn.style.top = cy + 'px';
        btn.style.right = 'auto';
        btn.style.bottom = 'auto';

        // Panel xếp theo vị trí nút: nếu nút ở nửa trên → panel xuống dưới; nửa dưới → panel lên trên
        const panelW = 380, panelH = 540;
        let px = cx + 60 - panelW;
        if (px < 8) px = cx;
        if (px + panelW > window.innerWidth - 8) px = window.innerWidth - panelW - 8;

        let py;
        if (cy > window.innerHeight / 2) py = cy - panelH - 8;
        else py = cy + 60 + 8;
        py = Math.max(8, Math.min(py, window.innerHeight - panelH - 8));

        panel.style.left = px + 'px';
        panel.style.top = py + 'px';
        panel.style.right = 'auto';
        panel.style.bottom = 'auto';
    }

    function loadBtnPos() {
        try {
            const saved = JSON.parse(localStorage.getItem(POS_KEY) || 'null');
            if (saved && typeof saved.x === 'number') {
                applyBtnPos(saved.x, saved.y);
                return true;
            }
        } catch {}
        return false;
    }

    function makeDraggable() {
        const btn = document.getElementById('adlv-chat-btn');
        let startX = 0, startY = 0, origX = 0, origY = 0, moved = false, dragging = false;

        const getPoint = (e) => {
            const t = e.touches ? e.touches[0] : e;
            return { x: t.clientX, y: t.clientY };
        };

        const onDown = (e) => {
            dragging = true;
            moved = false;
            const p = getPoint(e);
            startX = p.x;
            startY = p.y;
            const rect = btn.getBoundingClientRect();
            origX = rect.left;
            origY = rect.top;
            btn.style.transition = 'none';
            if (e.cancelable) e.preventDefault();
        };

        const onMove = (e) => {
            if (!dragging) return;
            const p = getPoint(e);
            const dx = p.x - startX;
            const dy = p.y - startY;
            if (!moved && Math.hypot(dx, dy) > 5) moved = true;
            if (moved) applyBtnPos(origX + dx, origY + dy);
        };

        const onUp = () => {
            if (!dragging) return;
            dragging = false;
            btn.style.transition = '';
            if (moved) {
                const rect = btn.getBoundingClientRect();
                try { localStorage.setItem(POS_KEY, JSON.stringify({ x: rect.left, y: rect.top })); } catch {}
            } else {
                isOpen ? closeChat() : openChat();
            }
        };

        btn.addEventListener('mousedown', onDown);
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onUp);
        btn.addEventListener('touchstart', onDown, { passive: false });
        document.addEventListener('touchmove', onMove, { passive: false });
        document.addEventListener('touchend', onUp);
    }

    function init() {
        if (document.getElementById('adlv-chat-btn')) return;
        injectStyles();
        injectHTML();
        loadBtnPos();
        makeDraggable();
        document.querySelector('#adlv-chat-panel .close').onclick = closeChat;
        document.getElementById('adlv-chat-form').onsubmit = (e) => {
            e.preventDefault();
            const input = document.getElementById('adlv-chat-input');
            handleUserMessage(input.value);
        };
        window.addEventListener('resize', () => {
            const btn = document.getElementById('adlv-chat-btn');
            if (btn.style.left) {
                const rect = btn.getBoundingClientRect();
                applyBtnPos(rect.left, rect.top);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
