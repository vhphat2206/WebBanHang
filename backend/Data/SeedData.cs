using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(ApplicationDbContext context)
        {
            if (!await context.Users.AnyAsync())
            {
                context.Users.Add(new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    FullName = "Quản trị viên",
                    Email = "admin@adlvstore.local",
                    Role = "Admin"
                });
                await context.SaveChangesAsync();
            }

            if (!await context.Categories.AnyAsync())
            {
                var ao = new Category { Name = "Áo" };
                var quan = new Category { Name = "Quần" };
                var giay = new Category { Name = "Giày" };
                var non = new Category { Name = "Nón" };

                context.Categories.AddRange(ao, quan, giay, non);
                await context.SaveChangesAsync();

                const string CDN = "https://dytbw3ui6vsu6.cloudfront.net/media/catalog/product/resize/914x1200";

                var products = new List<Product>
                {
                    new Product
                    {
                        Name = "Áo Thun ADLV Fuzzy Rabbit Đen",
                        Sku = "ADLV-23SS-SSBKZR-BLK",
                        Price = 1340000,
                        Description = "Áo thun ADLV họa tiết Fuzzy Rabbit lông xù 3D, dáng oversized unisex, chất cotton 100% mềm mịn, in lụa cao cấp giữ form lâu dài.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Oversized Unisex",
                        CareInstructions = "Khuyến nghị giặt khô. Giặt máy lộn ngược, dùng lưới giặt, nhiệt độ thấp, thời gian ngắn. Không dùng máy sấy.",
                        Notes = "Sản phẩm có thể có bột bảo vệ in hình - đây không phải lỗi.",
                        Sizes = "S,M,L,XL",
                        Colors = "Đen",
                        Stock = 50,
                        CategoryId = ao.Id,
                        ImageUrl = $"{CDN}/ADLV/000-ADLV-23SS-SSBKZR-BLK/000-ADLV-23SS-SSBKZR-BLK-006.webp",
                        IsBestseller = true
                    },
                    new Product
                    {
                        Name = "Áo Thun ADLV Fuzzy Rabbit Kem",
                        Sku = "ADLV-23SS-SSBKZR-CRM",
                        Price = 1340000,
                        Description = "Phiên bản màu kem nhẹ nhàng của Fuzzy Rabbit — họa tiết thỏ lông xù 3D nổi bật, dáng oversized.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Oversized Unisex",
                        Sizes = "S,M,L,XL",
                        Colors = "Kem",
                        Stock = 45,
                        CategoryId = ao.Id,
                        ImageUrl = $"{CDN}/ADLV/000-ADLV-23SS-SSBKZR-CRM/000-ADLV-23SS-SSBKZR-CRM-006.webp",
                        IsNew = true
                    },
                    new Product
                    {
                        Name = "Áo Thun ADLV Teddy Bear (Bear Doll) Đen",
                        Sku = "ADLV-21SS-SSADBK-TBD",
                        Price = 1340000,
                        SalePrice = 990000,
                        Description = "Áo thun ADLV họa tiết Teddy Bear Bear Doll — biểu tượng của thương hiệu, dáng oversized chuẩn streetwear.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Oversized Unisex",
                        Sizes = "S,M,L,XL",
                        Colors = "Đen",
                        Stock = 60,
                        CategoryId = ao.Id,
                        ImageUrl = $"{CDN}/ADLV/ADLV-21SS-SSADBK-TBD-006.webp",
                        IsBestseller = true
                    },
                    new Product
                    {
                        Name = "Áo Thun ADLV Basic 2 Trắng",
                        Sku = "ADLV-19SS-SSBLN2-WHT",
                        Price = 880000,
                        Description = "Áo thun ADLV Basic phiên bản 2 màu trắng tinh khôi — item must-have cho mọi outfit streetwear.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Regular Unisex",
                        Sizes = "S,M,L,XL",
                        Colors = "Trắng",
                        Stock = 80,
                        CategoryId = ao.Id,
                        ImageUrl = $"{CDN}/0/0/000-ADLV-19SS-SSBLN2-WHT-006_1_5.webp"
                    },
                    new Product
                    {
                        Name = "Áo Thun ADLV Basic Logo Season 2 Hồng",
                        Sku = "ADLV-23SS-SSLBSN-PNK",
                        Price = 1110000,
                        SalePrice = 880000,
                        Description = "Áo thun ADLV Basic Logo Season 2 màu hồng pastel ngọt ngào, in logo lụa cao cấp.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Regular Unisex",
                        Sizes = "S,M,L,XL",
                        Colors = "Hồng",
                        Stock = 40,
                        CategoryId = ao.Id,
                        ImageUrl = $"{CDN}/0/0/000-ADLV-23SS-SSLBSN-PNK-006_1_4.webp"
                    },
                    new Product
                    {
                        Name = "Áo Thun ADLV Triple Chain Embo Basic Logo Trắng",
                        Sku = "ADLV-26SS-TP-SS-LG-TCS-WHT",
                        Price = 1110000,
                        Description = "Phiên bản Triple Chain Embo 26SS — họa tiết dây xích thêu nổi 3 lớp trên nền trắng, item collector.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Oversized Unisex",
                        Sizes = "S,M,L,XL",
                        Colors = "Trắng",
                        Stock = 35,
                        CategoryId = ao.Id,
                        ImageUrl = $"{CDN}/ADLV/26SS-TP-SS-LG-TCS-WHT/26SS-TP-SS-LG-TCS-WHT-006.webp",
                        IsNew = true
                    },
                    new Product
                    {
                        Name = "Áo Thun ADLV Swirling AC Bear Trắng",
                        Sku = "ADLV-26SS-TP-SS-AW-SWS-WHT",
                        Price = 1340000,
                        Description = "Bộ sưu tập 26SS — họa tiết AC Bear xoáy nghệ thuật, in lụa nổi đặc biệt.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Oversized Unisex",
                        Sizes = "S,M,L,XL",
                        Colors = "Trắng",
                        Stock = 40,
                        CategoryId = ao.Id,
                        ImageUrl = $"{CDN}/ADLV/26SS-TP-SS-AW-SWS-WHT/26SS-TP-SS-AW-SWS-WHT-006.webp",
                        IsNew = true
                    },
                    new Product
                    {
                        Name = "Áo Thun ADLV In Script Logo Hồng",
                        Sku = "ADLV-23SS-SSLSCB-PNK",
                        Price = 1110000,
                        Description = "Áo thun ADLV in script logo viết tay phong cách, màu hồng ngọt ngào, hợp mix với jeans.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Regular Unisex",
                        Sizes = "S,M,L,XL",
                        Colors = "Hồng",
                        Stock = 50,
                        CategoryId = ao.Id,
                        ImageUrl = $"{CDN}/ADLV/000-ADLV-23SS-SSLSCB-PNK/000-ADLV-23SS-SSLSCB-PNK-006.webp"
                    },
                    new Product
                    {
                        Name = "Áo Thun ADLV Baby Face Donuts Đen",
                        Sku = "ADLV-21SS-SSBKBF-DN1",
                        Price = 1340000,
                        Description = "Bộ sưu tập Baby Face — họa tiết Donuts dễ thương, một biểu tượng iconic của ADLV.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Oversized Unisex",
                        Sizes = "S,M,L,XL",
                        Colors = "Đen",
                        Stock = 55,
                        CategoryId = ao.Id,
                        ImageUrl = $"{CDN}/0/0/000-ADLV-21SS-SSBKBF-DN1-006_1_6.webp"
                    }
                };

                context.Products.AddRange(products);
                await context.SaveChangesAsync();
            }
        }
    }
}
