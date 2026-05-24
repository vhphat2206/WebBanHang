/**
 * ADLV Cart Manager — localStorage-based shopping cart
 *
 * Public API:
 *   Cart.add({ productId, name, price, salePrice, image, brand, size, color, quantity })
 *   Cart.remove(productId, size, color)
 *   Cart.updateQty(productId, size, color, qty)
 *   Cart.clear()
 *   Cart.getItems()
 *   Cart.getCount()
 *   Cart.getSubtotal()
 *   Cart.openDrawer()
 *   Cart.closeDrawer()
 *   Cart.on(event, handler)  // 'change'
 */
(function () {
    const STORAGE_KEY = 'adlv_cart';
    const API_HOST = 'http://localhost:5083';

    const listeners = { change: [] };

    function load() {
        try {
            return JSON.parse(localStorage.getItem(STORAGE_KEY)) || [];
        } catch {
            return [];
        }
    }
    function save(items) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
        emit('change');
        updateBadges();
    }
    function emit(event) {
        (listeners[event] || []).forEach(fn => { try { fn(); } catch {} });
    }
    function key(p) {
        return `${p.productId}_${p.size || ''}_${p.color || ''}`;
    }
    function effectivePrice(item) {
        return item.salePrice ?? item.price;
    }

    const Cart = {
        getItems() { return load(); },

        getCount() {
            return load().reduce((sum, it) => sum + it.quantity, 0);
        },

        getSubtotal() {
            return load().reduce((sum, it) => sum + effectivePrice(it) * it.quantity, 0);
        },

        add(product) {
            const items = load();
            const k = key(product);
            const existing = items.find(it => key(it) === k);
            if (existing) {
                existing.quantity += product.quantity || 1;
            } else {
                items.push({
                    productId: product.productId,
                    name: product.name,
                    price: product.price,
                    salePrice: product.salePrice ?? null,
                    image: product.image || '',
                    brand: product.brand || 'ADLV',
                    size: product.size || '',
                    color: product.color || '',
                    quantity: product.quantity || 1
                });
            }
            save(items);
            toast(`Đã thêm ${product.quantity || 1} × ${product.name} vào giỏ`);
            this.openDrawer();
        },

        updateQty(productId, size, color, qty) {
            const items = load();
            const it = items.find(i => i.productId === productId && i.size === size && i.color === color);
            if (!it) return;
            if (qty <= 0) {
                this.remove(productId, size, color);
                return;
            }
            it.quantity = qty;
            save(items);
        },

        remove(productId, size, color) {
            const items = load().filter(i => !(i.productId === productId && i.size === size && i.color === color));
            save(items);
        },

        clear() { save([]); },

        on(event, fn) {
            if (!listeners[event]) listeners[event] = [];
            listeners[event].push(fn);
        },

        openDrawer() { drawer.open(); },
        closeDrawer() { drawer.close(); },
    };

    // ===== Helpers =====
    function fullImageUrl(url) {
        if (!url) return null;
        if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) return url;
        if (url.startsWith('/')) return API_HOST + url;
        return url;
    }
    function fmt(n) { return Math.round(n).toLocaleString('vi-VN') + '₫'; }

    function updateBadges() {
        const count = Cart.getCount();
        document.querySelectorAll('.cart-badge').forEach(el => {
            el.textContent = count;
            el.classList.toggle('cart-badge-hidden', count === 0);
        });
    }

    // ===== Toast =====
    function toast(msg, isError) {
        let el = document.getElementById('cartToast');
        if (!el) {
            el = document.createElement('div');
            el.id = 'cartToast';
            el.style.cssText = 'position:fixed;bottom:24px;right:24px;z-index:200;padding:14px 20px;border-radius:8px;color:white;font-weight:600;font-family:Inter,sans-serif;box-shadow:0 10px 30px rgba(0,0,0,0.2);transition:opacity 0.3s;';
            document.body.appendChild(el);
        }
        el.textContent = msg;
        el.style.background = isError ? '#DC2626' : '#111';
        el.style.opacity = '1';
        clearTimeout(toast._t);
        toast._t = setTimeout(() => { el.style.opacity = '0'; }, 3000);
    }

    // ===== Cart Drawer =====
    const drawer = {
        ensure() {
            if (document.getElementById('cartDrawer')) return;

            const overlay = document.createElement('div');
            overlay.id = 'cartDrawerOverlay';
            overlay.style.cssText = 'position:fixed;inset:0;background:rgba(0,0,0,0.5);backdrop-filter:blur(4px);z-index:99;opacity:0;pointer-events:none;transition:opacity 0.3s;';
            overlay.onclick = () => drawer.close();

            const panel = document.createElement('div');
            panel.id = 'cartDrawer';
            panel.style.cssText = 'position:fixed;top:0;right:0;bottom:0;width:100%;max-width:440px;background:white;z-index:100;transform:translateX(100%);transition:transform 0.35s ease;display:flex;flex-direction:column;font-family:Inter,sans-serif;';

            panel.innerHTML = `
                <div style="padding:24px 24px 16px;border-bottom:1px solid #E5E7EB;display:flex;justify-content:space-between;align-items:center;">
                    <div>
                        <div style="font-weight:900;text-transform:uppercase;letter-spacing:1px;font-size:18px;">Giỏ hàng</div>
                        <div id="cartDrawerCount" style="font-size:12px;color:#6B7280;margin-top:2px;">0 sản phẩm</div>
                    </div>
                    <button onclick="Cart.closeDrawer()" style="width:36px;height:36px;border-radius:50%;background:#F3F4F6;border:none;font-size:20px;cursor:pointer;">×</button>
                </div>
                <div id="cartDrawerItems" style="flex:1;overflow-y:auto;padding:8px 24px;"></div>
                <div id="cartDrawerFooter" style="border-top:1px solid #E5E7EB;padding:20px 24px;background:#FAFAFA;">
                    <div style="display:flex;justify-content:space-between;margin-bottom:4px;font-size:13px;color:#6B7280;">
                        <span>Tạm tính</span>
                        <span id="cartDrawerSubtotal" style="font-weight:600;color:#111;">0₫</span>
                    </div>
                    <div style="font-size:11px;color:#9CA3AF;margin-bottom:14px;">Phí vận chuyển và mã giảm giá tính ở bước thanh toán</div>
                    <a href="cart.html" style="display:block;text-align:center;border:2px solid #111;color:#111;padding:12px;font-weight:700;text-transform:uppercase;font-size:13px;text-decoration:none;margin-bottom:8px;">Xem giỏ hàng</a>
                    <a href="checkout.html" style="display:block;text-align:center;background:#111;color:white;padding:12px;font-weight:700;text-transform:uppercase;font-size:13px;text-decoration:none;" onmouseover="this.style.background='#DC2626'" onmouseout="this.style.background='#111'">Thanh toán</a>
                </div>`;

            document.body.appendChild(overlay);
            document.body.appendChild(panel);
        },

        render() {
            const items = Cart.getItems();
            const wrap = document.getElementById('cartDrawerItems');
            const count = document.getElementById('cartDrawerCount');
            const sub = document.getElementById('cartDrawerSubtotal');
            const footer = document.getElementById('cartDrawerFooter');

            count.textContent = `${Cart.getCount()} sản phẩm`;

            if (!items.length) {
                wrap.innerHTML = `<div style="text-align:center;padding:60px 20px;color:#9CA3AF;">
                    <svg style="width:64px;height:64px;margin:0 auto 12px;display:block;" fill="none" stroke="#D1D5DB" stroke-width="1.5" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"/></svg>
                    <div style="font-weight:600;color:#6B7280;">Giỏ hàng trống</div>
                    <div style="font-size:12px;margin-top:6px;">Khám phá sản phẩm để bắt đầu mua sắm</div>
                </div>`;
                footer.style.display = 'none';
                sub.textContent = '0₫';
                return;
            }

            footer.style.display = 'block';
            wrap.innerHTML = items.map((it, idx) => {
                const img = fullImageUrl(it.image);
                const price = effectivePrice(it);
                return `
                <div style="display:flex;gap:14px;padding:16px 0;border-bottom:1px solid #F3F4F6;">
                    <div style="width:80px;height:80px;flex-shrink:0;background:#F3F4F6;overflow:hidden;">
                        ${img
                            ? `<img src="${img}" style="width:100%;height:100%;object-fit:cover;"/>`
                            : `<div style="width:100%;height:100%;background:linear-gradient(135deg,#1F2937,#4B5563);display:flex;align-items:center;justify-content:center;color:white;font-weight:900;font-size:24px;">${(it.brand || it.name).charAt(0)}</div>`}
                    </div>
                    <div style="flex:1;min-width:0;">
                        <div style="font-size:10px;text-transform:uppercase;letter-spacing:1px;color:#9CA3AF;font-weight:600;">${it.brand || 'ADLV'}</div>
                        <div style="font-size:13px;font-weight:600;line-height:1.3;margin:2px 0 4px;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;">${it.name}</div>
                        <div style="font-size:11px;color:#6B7280;">
                            ${it.size ? `Size: <b>${it.size}</b>` : ''}
                            ${it.size && it.color ? ' · ' : ''}
                            ${it.color ? `Màu: <b>${it.color}</b>` : ''}
                        </div>
                        <div style="display:flex;justify-content:space-between;align-items:center;margin-top:8px;">
                            <div style="display:inline-flex;border:1px solid #E5E7EB;border-radius:4px;">
                                <button onclick="Cart.updateQty(${it.productId},'${(it.size||'').replace(/'/g,"\\'")}','${(it.color||'').replace(/'/g,"\\'")}',${it.quantity-1})" style="width:28px;height:28px;background:white;border:none;font-weight:700;cursor:pointer;">−</button>
                                <span style="width:36px;text-align:center;line-height:28px;font-weight:600;font-size:13px;">${it.quantity}</span>
                                <button onclick="Cart.updateQty(${it.productId},'${(it.size||'').replace(/'/g,"\\'")}','${(it.color||'').replace(/'/g,"\\'")}',${it.quantity+1})" style="width:28px;height:28px;background:white;border:none;font-weight:700;cursor:pointer;">+</button>
                            </div>
                            <div style="font-weight:700;font-size:14px;">${fmt(price * it.quantity)}</div>
                        </div>
                    </div>
                    <button onclick="Cart.remove(${it.productId},'${(it.size||'').replace(/'/g,"\\'")}','${(it.color||'').replace(/'/g,"\\'")}')" title="Xóa" style="background:none;border:none;color:#9CA3AF;cursor:pointer;align-self:flex-start;font-size:18px;line-height:1;">×</button>
                </div>`;
            }).join('');

            sub.textContent = fmt(Cart.getSubtotal());
        },

        open() {
            this.ensure();
            this.render();
            const overlay = document.getElementById('cartDrawerOverlay');
            const panel = document.getElementById('cartDrawer');
            overlay.style.opacity = '1';
            overlay.style.pointerEvents = 'auto';
            panel.style.transform = 'translateX(0)';
            document.body.style.overflow = 'hidden';
        },

        close() {
            const overlay = document.getElementById('cartDrawerOverlay');
            const panel = document.getElementById('cartDrawer');
            if (!overlay || !panel) return;
            overlay.style.opacity = '0';
            overlay.style.pointerEvents = 'none';
            panel.style.transform = 'translateX(100%)';
            document.body.style.overflow = '';
        }
    };

    // Re-render drawer when cart changes (if open)
    Cart.on('change', () => {
        if (document.getElementById('cartDrawer')?.style.transform === 'translateX(0px)') {
            drawer.render();
        }
    });

    // ===== Wishlist (đơn giản: array of productId trong localStorage) =====
    const WISHLIST_KEY = 'adlv_wishlist';
    const wishListeners = [];

    const Wishlist = {
        getAll() {
            try { return JSON.parse(localStorage.getItem(WISHLIST_KEY)) || []; } catch { return []; }
        },
        has(productId) {
            return this.getAll().includes(parseInt(productId));
        },
        add(productId) {
            const id = parseInt(productId);
            const list = this.getAll();
            if (!list.includes(id)) {
                list.push(id);
                localStorage.setItem(WISHLIST_KEY, JSON.stringify(list));
                this._notify();
            }
        },
        remove(productId) {
            const id = parseInt(productId);
            const list = this.getAll().filter(x => x !== id);
            localStorage.setItem(WISHLIST_KEY, JSON.stringify(list));
            this._notify();
        },
        toggle(productId) {
            if (this.has(productId)) {
                this.remove(productId);
                toast('Đã xóa khỏi yêu thích');
                return false;
            } else {
                this.add(productId);
                toast('💖 Đã thêm vào yêu thích');
                return true;
            }
        },
        getCount() { return this.getAll().length; },
        on(fn) { wishListeners.push(fn); },
        _notify() {
            wishListeners.forEach(fn => { try { fn(); } catch {} });
            updateWishlistBadges();
        }
    };

    function updateWishlistBadges() {
        const count = Wishlist.getCount();
        document.querySelectorAll('.wishlist-badge').forEach(el => {
            el.textContent = count;
            el.style.display = count === 0 ? 'none' : '';
        });
        // Update heart buttons
        document.querySelectorAll('[data-wishlist-id]').forEach(btn => {
            const id = parseInt(btn.dataset.wishlistId);
            const active = Wishlist.has(id);
            btn.classList.toggle('wishlist-active', active);
            const svg = btn.querySelector('svg');
            if (svg) {
                svg.setAttribute('fill', active ? '#DC2626' : 'none');
                svg.setAttribute('stroke', active ? '#DC2626' : '#111');
            }
        });
    }

    // ===== Notifications (bell dropdown) =====
    const NOTIF_READ_KEY = 'adlv_notif_read';
    const Notifications = {
        list: [],
        getReadSet() {
            try { return new Set(JSON.parse(localStorage.getItem(NOTIF_READ_KEY)) || []); } catch { return new Set(); }
        },
        saveReadSet(set) {
            localStorage.setItem(NOTIF_READ_KEY, JSON.stringify([...set]));
        },
        markRead(id) {
            const s = this.getReadSet();
            s.add(id);
            this.saveReadSet(s);
            updateNotifBadge();
        },
        markAllRead() {
            const s = this.getReadSet();
            this.list.forEach(n => s.add(n.id));
            this.saveReadSet(s);
            updateNotifBadge();
            notifDropdown.render();
        },
        getUnreadCount() {
            const s = this.getReadSet();
            return this.list.filter(n => !s.has(n.id)).length;
        },
        async load() {
            const token = localStorage.getItem('admin_token');
            if (!token) { this.list = []; updateNotifBadge(); return; }
            const wishlist = Wishlist.getAll().join(',');
            try {
                const res = await fetch(`${API_HOST}/api/notifications/my?wishlist=${wishlist}`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                if (!res.ok) { this.list = []; return; }
                this.list = await res.json();
                updateNotifBadge();
            } catch { this.list = []; }
        },
        openDropdown() { notifDropdown.open(); },
        closeDropdown() { notifDropdown.close(); }
    };

    function updateNotifBadge() {
        const count = Notifications.getUnreadCount();
        document.querySelectorAll('.notif-badge').forEach(el => {
            el.textContent = count;
            el.style.display = count === 0 ? 'none' : '';
        });
    }

    function timeAgo(iso) {
        const d = new Date(iso);
        const diff = (Date.now() - d.getTime()) / 1000;
        if (diff < 60) return 'vừa xong';
        if (diff < 3600) return Math.floor(diff / 60) + ' phút trước';
        if (diff < 86400) return Math.floor(diff / 3600) + ' giờ trước';
        if (diff < 86400 * 7) return Math.floor(diff / 86400) + ' ngày trước';
        return d.toLocaleDateString('vi-VN');
    }

    const notifDropdown = {
        ensure() {
            if (document.getElementById('notifDropdown')) return;
            const overlay = document.createElement('div');
            overlay.id = 'notifDropdownOverlay';
            overlay.style.cssText = 'position:fixed;inset:0;z-index:90;display:none;';
            overlay.onclick = () => notifDropdown.close();

            const panel = document.createElement('div');
            panel.id = 'notifDropdown';
            panel.style.cssText = 'position:fixed;top:64px;right:24px;width:380px;max-width:calc(100vw - 48px);max-height:calc(100vh - 100px);background:white;border:1px solid #E5E7EB;border-radius:12px;box-shadow:0 20px 50px rgba(0,0,0,0.15);z-index:100;display:none;flex-direction:column;font-family:Inter,sans-serif;overflow:hidden;';

            panel.innerHTML = `
                <div style="padding:16px 20px;border-bottom:1px solid #E5E7EB;display:flex;justify-content:space-between;align-items:center;">
                    <div style="font-weight:900;text-transform:uppercase;letter-spacing:1px;font-size:14px;">Thông báo</div>
                    <button onclick="Notifications.markAllRead()" style="font-size:11px;color:#6B7280;background:none;border:none;cursor:pointer;text-decoration:underline;">Đánh dấu đã đọc tất cả</button>
                </div>
                <div id="notifList" style="flex:1;overflow-y:auto;"></div>`;

            document.body.appendChild(overlay);
            document.body.appendChild(panel);
        },
        render() {
            const wrap = document.getElementById('notifList');
            const readSet = Notifications.getReadSet();
            if (!Notifications.list.length) {
                wrap.innerHTML = `<div style="padding:48px 20px;text-align:center;color:#9CA3AF;">
                    <svg style="width:48px;height:48px;margin:0 auto 8px;display:block;" fill="none" stroke="#D1D5DB" stroke-width="1.5" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/></svg>
                    <div style="font-weight:600;color:#6B7280;font-size:13px;">Chưa có thông báo</div>
                </div>`;
                return;
            }
            wrap.innerHTML = Notifications.list.map(n => {
                const unread = !readSet.has(n.id);
                const imgUrl = n.image
                    ? (n.image.startsWith('http') ? n.image : (n.image.startsWith('/') ? API_HOST + n.image : n.image))
                    : null;
                return `
                <a href="${n.link || '#'}" onclick="Notifications.markRead('${n.id}')"
                    style="display:flex;gap:12px;padding:12px 20px;border-bottom:1px solid #F3F4F6;text-decoration:none;color:inherit;${unread ? 'background:#F0F9FF;' : ''}transition:background 0.15s;"
                    onmouseover="this.style.background='#F9FAFB'"
                    onmouseout="this.style.background='${unread ? '#F0F9FF' : 'white'}'">
                    <div style="font-size:24px;flex-shrink:0;">${n.icon || '🔔'}</div>
                    <div style="flex:1;min-width:0;">
                        <div style="font-weight:600;font-size:13px;line-height:1.3;${unread ? 'color:#111;' : 'color:#6B7280;'}">${n.title}</div>
                        <div style="font-size:11px;color:#6B7280;margin-top:2px;line-height:1.4;">${n.message}</div>
                        <div style="font-size:10px;color:#9CA3AF;margin-top:4px;">${timeAgo(n.timestamp)}</div>
                    </div>
                    ${unread ? '<div style="width:8px;height:8px;background:#3B82F6;border-radius:50%;flex-shrink:0;margin-top:6px;"></div>' : ''}
                </a>`;
            }).join('');
        },
        open() {
            this.ensure();
            this.render();
            document.getElementById('notifDropdownOverlay').style.display = 'block';
            document.getElementById('notifDropdown').style.display = 'flex';
        },
        close() {
            const overlay = document.getElementById('notifDropdownOverlay');
            const panel = document.getElementById('notifDropdown');
            if (overlay) overlay.style.display = 'none';
            if (panel) panel.style.display = 'none';
        }
    };

    // Expose
    window.Cart = Cart;
    window.Wishlist = Wishlist;
    window.Notifications = Notifications;

    // Update badges on load + cross-tab sync
    window.addEventListener('storage', (e) => {
        if (e.key === STORAGE_KEY) updateBadges();
    });
    function onReady() {
        updateBadges();
        updateWishlistBadges();
        Notifications.load();
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', onReady);
    } else {
        onReady();
    }
})();
