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

                var products = new List<Product>
                {
                    new Product
                    {
                        Name = "Áo Thun ADLV Teddy Bear (Bear Doll) Đen",
                        Sku = "ADLV-21SS-SSADBK-TBD",
                        Price = 1340000,
                        Description = "Áo thun ADLV họa tiết Teddy Bear, dáng oversized unisex, cotton 100%.",
                        Brand = "ADLV",
                        Material = "Cotton 100%",
                        Fit = "Oversized Unisex",
                        CareInstructions = "Khuyến nghị giặt khô. Giặt máy lộn ngược, dùng lưới giặt, nhiệt độ thấp, thời gian ngắn. Không dùng máy sấy.",
                        Notes = "Sản phẩm có thể có bột bảo vệ in hình - đây không phải lỗi.",
                        Sizes = "S,M,L,XL",
                        Colors = "Đen,Trắng,Xanh Da Trời",
                        Stock = 50,
                        CategoryId = ao.Id,
                        ImageUrl = "ao_adlv_teddy.jpg",
                        IsBestseller = true
                    },
                    new Product
                    {
                        Name = "Áo Thun Nike Jordan T-Shirt",
                        Sku = "NK-JD-TSH-001",
                        Price = 550000,
                        Description = "Áo thun cotton chính hãng Jordan",
                        Brand = "Jordan",
                        Material = "Cotton 100%",
                        Fit = "Regular",
                        Sizes = "S,M,L,XL",
                        Colors = "Đen,Trắng",
                        Stock = 50,
                        CategoryId = ao.Id,
                        ImageUrl = "ao_jordan.jpg",
                        IsNew = true
                    },
                    new Product
                    {
                        Name = "Quần Short Thể Thao Jordan Pro",
                        Sku = "NK-JD-SH-002",
                        Price = 650000,
                        Description = "Quần short thoáng khí thương hiệu Jordan Air",
                        Brand = "Jordan",
                        Material = "Polyester",
                        Fit = "Regular",
                        Sizes = "M,L,XL",
                        Colors = "Đen,Xám",
                        Stock = 30,
                        CategoryId = quan.Id,
                        ImageUrl = "quan_jordan.jpg"
                    },
                    new Product
                    {
                        Name = "Quần Dài Jean Levi's Slim Fit",
                        Sku = "LV-511-SLM",
                        Price = 1200000,
                        Description = "Quần jean chất bò cao cấp",
                        Brand = "Levi's",
                        Material = "Denim",
                        Fit = "Slim Fit",
                        Sizes = "29,30,31,32,33",
                        Colors = "Xanh đậm,Đen",
                        Stock = 25,
                        CategoryId = quan.Id,
                        ImageUrl = "quan_jean.jpg"
                    },
                    new Product
                    {
                        Name = "Giày Sneaker Air Jordan 1 Low",
                        Sku = "NK-AJ1-LOW",
                        Price = 3500000,
                        SalePrice = 2999000,
                        Description = "Giày cổ thấp phối màu đen trắng hot trend",
                        Brand = "Nike",
                        Material = "Da",
                        Fit = "True to size",
                        Sizes = "39,40,41,42,43",
                        Colors = "Đen-Trắng,Đỏ-Trắng",
                        Stock = 20,
                        CategoryId = giay.Id,
                        ImageUrl = "giay_jordan.jpg",
                        IsBestseller = true
                    },
                    new Product
                    {
                        Name = "Giày Adidas Ultraboost 22",
                        Sku = "AD-UB22",
                        Price = 4200000,
                        Description = "Giày chạy bộ êm chân công nghệ Boost",
                        Brand = "Adidas",
                        Material = "Primeknit",
                        Fit = "True to size",
                        Sizes = "39,40,41,42,43,44",
                        Colors = "Đen,Trắng,Xám",
                        Stock = 15,
                        CategoryId = giay.Id,
                        ImageUrl = "giay_adidas.jpg",
                        IsNew = true
                    },
                    new Product
                    {
                        Name = "Dép Sandal Nike Calm Slide",
                        Sku = "NK-CALM-SLD",
                        Price = 950000,
                        Description = "Dép quai ngang êm ái, chống trượt",
                        Brand = "Nike",
                        Material = "EVA",
                        Sizes = "39,40,41,42,43",
                        Colors = "Đen,Nâu",
                        Stock = 40,
                        CategoryId = giay.Id,
                        ImageUrl = "dep_nike.jpg"
                    },
                    new Product
                    {
                        Name = "Nón Lưỡi Trai Adidas Originals",
                        Sku = "AD-CAP-ORG",
                        Price = 350000,
                        Description = "Nón kết thể thao Adidas",
                        Brand = "Adidas",
                        Material = "Cotton",
                        Sizes = "Freesize",
                        Colors = "Đen,Trắng,Xanh",
                        Stock = 60,
                        CategoryId = non.Id,
                        ImageUrl = "non_adidas.jpg"
                    }
                };

                context.Products.AddRange(products);
                await context.SaveChangesAsync();
            }
        }
    }
}
