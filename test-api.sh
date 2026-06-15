#!/bin/bash
HOST="https://adlv-shop.onrender.com"
PASS=0; FAIL=0

test_endpoint() {
  local desc="$1"
  local expected="$2"
  local actual="$3"
  if [[ "$actual" == "$expected" ]]; then
    echo "✅ $desc [$actual]"; PASS=$((PASS+1))
  else
    echo "❌ $desc [expected:$expected got:$actual]"; FAIL=$((FAIL+1))
  fi
}

echo "═════════════════ TEST 60+ API ENDPOINTS ═════════════════"
echo ""

# === SETUP ===
ADMIN_TOKEN=$(curl -s -X POST $HOST/api/auth/login -H "Content-Type: application/json" -d '{"username":"admin","password":"admin123"}' | python3 -c "import sys,json; print(json.load(sys.stdin).get('token',''))" 2>/dev/null)
USER_TOKEN=$(curl -s -X POST $HOST/api/auth/login -H "Content-Type: application/json" -d '{"username":"elonmusk","password":"musk2026"}' | python3 -c "import sys,json; print(json.load(sys.stdin).get('token',''))" 2>/dev/null)
USER_REFRESH=$(curl -s -X POST $HOST/api/auth/login -H "Content-Type: application/json" -d '{"username":"elonmusk","password":"musk2026"}' | python3 -c "import sys,json; print(json.load(sys.stdin).get('refreshToken',''))" 2>/dev/null)

# === 1. AUTH ===
echo "── 1. AUTH ──"
test_endpoint "POST /auth/login (admin OK)" "200" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' -d '{"username":"admin","password":"admin123"}' $HOST/api/auth/login)"
test_endpoint "POST /auth/login (sai pass → 401)" "401" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' -d '{"username":"admin","password":"sai"}' $HOST/api/auth/login)"
test_endpoint "GET /auth/me (có token)" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/auth/me)"
test_endpoint "GET /auth/me (không token → 401)" "401" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/api/auth/me)"
test_endpoint "GET /auth/stats" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/auth/stats)"
test_endpoint "POST /auth/forgot-password" "200" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' -d '{"email":"elon@spacex.com"}' $HOST/api/auth/forgot-password)"
test_endpoint "POST /auth/forgot-password (email sai)" "404" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' -d '{"email":"khongco@x.com"}' $HOST/api/auth/forgot-password)"
test_endpoint "POST /auth/refresh" "200" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' -d "{\"refreshToken\":\"$USER_REFRESH\"}" $HOST/api/auth/refresh)"

# === 2. PRODUCTS ===
echo ""
echo "── 2. PRODUCTS ──"
test_endpoint "GET /products" "200" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/api/products)"
test_endpoint "GET /products/1" "200" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/api/products/1)"
test_endpoint "GET /products/search?query=fuzzy" "200" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/api/products/search?query=fuzzy)"
test_endpoint "GET filter ?categoryId=1" "200" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/api/products?categoryId=1)"
test_endpoint "GET sort ?sort=price_asc" "200" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/api/products?sort=price_asc)"
test_endpoint "GET pagination ?page=1&pageSize=5" "200" "$(curl -s -o /dev/null -w '%{http_code}' "$HOST/api/products?page=1&pageSize=5")"
test_endpoint "POST product (admin)" "201" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' -d '{"name":"AutoTest","price":100000,"categoryId":1,"brand":"ADLV"}' $HOST/api/products)"
test_endpoint "POST product (validation: giá âm → 400)" "400" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' -d '{"name":"X","price":-1,"categoryId":1,"brand":"ADLV"}' $HOST/api/products)"
test_endpoint "POST product (customer → 403)" "403" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $USER_TOKEN" -H 'Content-Type: application/json' -d '{"name":"X","price":1,"categoryId":1,"brand":"ADLV"}' $HOST/api/products)"

# === 3. CATEGORIES ===
echo ""
echo "── 3. CATEGORIES ──"
test_endpoint "GET /categories" "200" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/api/categories)"
test_endpoint "GET /categories/1" "200" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/api/categories/1)"
test_endpoint "DELETE category 1 (còn SP → 400)" "400" "$(curl -s -o /dev/null -w '%{http_code}' -X DELETE -H "Authorization: Bearer $ADMIN_TOKEN" $HOST/api/categories/1)"

# === 4. CART ===
echo ""
echo "── 4. CART ──"
test_endpoint "GET /cart" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/cart)"
test_endpoint "POST /cart/items" "200" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $USER_TOKEN" -H 'Content-Type: application/json' -d '{"productId":1,"quantity":2,"size":"M","color":"Đen"}' $HOST/api/cart/items)"
test_endpoint "GET /cart/total" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/cart/total)"
test_endpoint "DELETE /cart (clear)" "200" "$(curl -s -o /dev/null -w '%{http_code}' -X DELETE -H "Authorization: Bearer $USER_TOKEN" $HOST/api/cart)"

# === 5. ORDERS ===
echo ""
echo "── 5. ORDERS ──"
# Setup: add item rồi đặt
curl -s -X POST -H "Authorization: Bearer $USER_TOKEN" -H 'Content-Type: application/json' -d '{"productId":1,"quantity":1,"size":"M","color":"Đen"}' $HOST/api/cart/items > /dev/null
test_endpoint "POST /orders/from-cart" "200" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $USER_TOKEN" -H 'Content-Type: application/json' -d '{"customerName":"Elon","phone":"0901234567","shippingAddress":"1 Rocket","paymentMethod":"COD"}' $HOST/api/orders/from-cart)"
test_endpoint "POST /orders/from-cart (cart trống → 400)" "400" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $USER_TOKEN" -H 'Content-Type: application/json' -d '{"customerName":"X","phone":"0","shippingAddress":"X"}' $HOST/api/orders/from-cart)"
test_endpoint "GET /orders/my" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/orders/my)"
test_endpoint "GET /orders (admin all)" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $ADMIN_TOKEN" $HOST/api/orders)"
test_endpoint "GET /orders (customer → 403)" "403" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/orders)"
test_endpoint "GET /orders/vouchers" "200" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/api/orders/vouchers)"
test_endpoint "GET /orders/my-vouchers" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/orders/my-vouchers)"

# === 6. PAYMENTS ===
echo ""
echo "── 6. PAYMENTS ──"
test_endpoint "GET /payments" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/payments)"

# === 7. USERS (Admin) ===
echo ""
echo "── 7. USERS (Admin) ──"
test_endpoint "GET /users (admin)" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $ADMIN_TOKEN" $HOST/api/users)"
test_endpoint "GET /users (customer → 403)" "403" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/users)"
test_endpoint "GET /users/2" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $ADMIN_TOKEN" $HOST/api/users/2)"

# === 8. UPLOAD (Cloudinary) ===
echo ""
echo "── 8. UPLOAD ──"
curl -s -o /tmp/test.png https://www.google.com/images/branding/googlelogo/1x/googlelogo_color_92x30dp.png
test_endpoint "POST /upload (Cloudinary)" "200" "$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $USER_TOKEN" -F "file=@/tmp/test.png" $HOST/api/upload)"

# === 9. NOTIFICATIONS ===
echo ""
echo "── 9. NOTIFICATIONS ──"
test_endpoint "GET /notifications/my" "200" "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $USER_TOKEN" $HOST/api/notifications/my)"

# === SUMMARY ===
echo ""
echo "═════════════════ TỔNG KẾT ═════════════════"
TOTAL=$((PASS+FAIL))
echo "✅ PASS: $PASS / $TOTAL"
[ $FAIL -gt 0 ] && echo "❌ FAIL: $FAIL / $TOTAL" || echo "🎉 PERFECT — 100% endpoints hoạt động!"
