# ADLV Store — Web Bán Hàng Full-stack

Dự án web bán quần áo, giày dép theo phong cách streetwear ADLV (acmedelavie.com.vn). Đồ án sinh viên, full-stack: .NET 8 Web API + Frontend HTML thuần.

---

## Stack

| Layer | Công nghệ |
|-------|-----------|
| Backend | ASP.NET Core 8 Web API |
| Database | SQLite + Entity Framework Core 8 |
| Auth | JWT Bearer + BCrypt.Net (hash mật khẩu) |
| Frontend | HTML5 + Tailwind CSS (CDN) + JS thuần |
| Font | Inter (Google Fonts) |

---

## Tính năng

### Phía khách hàng
- Trang chủ với hero banner, marquee promo, các section **Bán Chạy / Hàng Mới / Bộ Sưu Tập**
- Trang danh mục riêng cho từng loại: `category.html?type=ao|quan|giay|phu-kien|moi|ban-chay|sale|all`
- Sidebar filter: khoảng giá, size, thương hiệu + sắp xếp (mới / giá tăng / giá giảm / A-Z)
- Trang chi tiết sản phẩm: gallery ảnh zoom, selector size + màu, số lượng, tabs Mô tả / Thông số / Bảo quản / Lưu ý, sản phẩm tương tự
- Tìm kiếm full-text theo tên, mô tả, thương hiệu, SKU
- Hiển thị badge `NEW` / `HOT` / `SALE` theo flag DB

### Phía admin
- Đăng nhập JWT, lưu token localStorage, auto-redirect dashboard khi đã login
- Quản lý sản phẩm: bảng liệt kê, modal form đầy đủ field chuẩn ADLV
- Chip input cho **Sizes** và **Colors** (gõ rồi Enter để thêm, click × để xóa)
- Quản lý ảnh: dán URL (kèm preview real-time) hoặc upload file lên server
- Quản lý danh mục: thêm / sửa / xóa
- Phân quyền `[Authorize(Roles=Admin)]` chặn mọi thao tác chỉnh sửa nếu không có token admin

---

## Cách chạy

### Yêu cầu
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- Trình duyệt hiện đại (Chrome, Safari, Firefox)

### Backend
```bash
cd backend
dotnet run
```
- API: `http://localhost:5083`
- Swagger UI: `http://localhost:5083/swagger`
- Database SQLite (`fashionshop.db`) sẽ được tự tạo và seed dữ liệu mẫu khi chạy lần đầu

### Frontend
Mở trực tiếp file HTML trong trình duyệt:
```bash
open frontend/index.html        # Trang chủ
open frontend/admin.html        # Dashboard admin
```

Hoặc serve qua HTTP nhẹ (tùy chọn):
```bash
cd frontend && python3 -m http.server 8080
# Truy cập http://localhost:8080
```

### Tài khoản admin mặc định
```
Username: admin
Password: admin123
```

---

## API Endpoints

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| `POST` | `/api/auth/login` | — | Đăng nhập, trả JWT token |
| `POST` | `/api/auth/register` | — | Đăng ký user mới (role Customer) |
| `GET` | `/api/products` | — | Danh sách sản phẩm |
| `GET` | `/api/products/{id}` | — | Chi tiết sản phẩm |
| `GET` | `/api/products/search?query=...` | — | Tìm kiếm full-text |
| `POST` | `/api/products` | Admin | Thêm sản phẩm |
| `PUT` | `/api/products/{id}` | Admin | Cập nhật |
| `DELETE` | `/api/products/{id}` | Admin | Xóa |
| `GET` | `/api/categories` | — | Danh sách danh mục |
| `POST` `PUT` `DELETE` | `/api/categories[/{id}]` | Admin | CRUD danh mục |
| `POST` | `/api/upload` | Admin | Upload ảnh (multipart, ≤5MB) |

---

## Cấu trúc thư mục

```
WebBanHang/
├── backend/
│   ├── Controllers/
│   │   ├── AuthController.cs           # Login + Register
│   │   ├── ProductsController.cs       # CRUD + Search sản phẩm
│   │   ├── CategoriesController.cs     # CRUD danh mục
│   │   └── UploadController.cs         # Upload ảnh
│   ├── Models/
│   │   ├── Product.cs                  # SKU, SalePrice, Material, Fit, ImageUrls...
│   │   ├── Category.cs
│   │   └── User.cs                     # Username, PasswordHash, Role
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   └── SeedData.cs                 # Khởi tạo admin + 7 SP mẫu
│   ├── Services/
│   │   └── JwtService.cs               # Sinh JWT token
│   ├── wwwroot/uploads/                # Ảnh do admin upload (gitignored)
│   ├── Program.cs                      # Cấu hình DI, JWT, CORS, StaticFiles
│   └── appsettings.json
│
└── frontend/
    ├── index.html                      # Trang chủ
    ├── admin.html                      # Dashboard admin
    ├── category.html                   # Trang danh mục với filter
    ├── product.html                    # Trang chi tiết sản phẩm
    └── assets/                         # Ảnh static
```

---

## Schema database

### Bảng `Products`
| Field | Kiểu | Ghi chú |
|-------|------|---------|
| Id | INTEGER PK | |
| Name | TEXT | |
| Sku | TEXT | Mã sản phẩm, format ADLV-XXSS-XXXX |
| Price | DECIMAL | Giá gốc |
| SalePrice | DECIMAL? | Giá khuyến mãi (nullable) |
| Description | TEXT | |
| ImageUrl | TEXT | URL ảnh chính |
| ImageUrls | TEXT | URL ảnh phụ (phân cách bằng dấu phẩy) |
| Brand | TEXT | Mặc định "ADLV" |
| Material | TEXT | VD: "Cotton 100%" |
| Fit | TEXT | VD: "Oversized Unisex" |
| CareInstructions | TEXT | Hướng dẫn bảo quản |
| Notes | TEXT | Ghi chú thêm |
| Sizes | TEXT | Danh sách size, phân cách dấu phẩy |
| Colors | TEXT | Danh sách màu, phân cách dấu phẩy |
| Stock | INT | Tồn kho |
| IsNew | BOOL | Hiển thị badge NEW |
| IsBestseller | BOOL | Hiển thị badge HOT |
| CategoryId | INT FK | → Categories |
| CreatedAt / UpdatedAt | DATETIME | |

### Bảng `Categories`
- Id, Name (Áo / Quần / Giày / Nón)
- Quan hệ 1-N với Products (Cascade Delete)

### Bảng `Users`
- Id, Username (unique), PasswordHash (BCrypt), FullName, Email, Role (Admin/Customer), CreatedAt

---

## Lưu ý cho production

- ⚠️ JWT secret hiện đang để công khai trong `appsettings.json`. Production cần chuyển sang **User Secrets** hoặc biến môi trường:
  ```bash
  dotnet user-secrets init
  dotnet user-secrets set "Jwt:Key" "<key-mới-bảo-mật>"
  ```
- Đổi `admin123` thành mật khẩu mạnh hơn ngay sau lần chạy đầu
- SQLite phù hợp cho đồ án; production nên dùng PostgreSQL / SQL Server
- CORS hiện đang `AllowAnyOrigin` — production cần whitelist domain frontend cụ thể

---

## Tham khảo giao diện

- ADLV official: https://acmedelavie.com.vn
- AusyncLab: https://ausynclab.io (lấy cảm hứng gradient + glassmorphism cho phiên bản trước)
