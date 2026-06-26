using Microsoft.EntityFrameworkCore;
using TrainingBackend.Data;
using TrainingBackend.Dtos;
using TrainingBackend.Entities;
using TrainingBackend.Exceptions;
using TrainingBackend.Repositories;
using TrainingBackend.Services;
using Xunit;

namespace TrainingBackend.Tests;

public class OrderServiceTests
{
    // テストごとに独立したインメモリ DB を用意する
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AppDbContext SeededContext()
    {
        var db = CreateContext();

        db.Products.AddRange(
            new Product { Id = 1, Name = "ノート", Price = 300m, Stock = 50 },
            new Product { Id = 2, Name = "マグカップ", Price = 1200m, Stock = 20 },
            new Product { Id = 3, Name = "ステッカー", Price = 200m, Stock = 0 }); // 在庫切れ

        db.Coupons.AddRange(
            new Coupon { Id = 1, Code = "WELCOME500", DiscountType = DiscountType.FixedAmount, DiscountValue = 500m },
            new Coupon { Id = 2, Code = "SALE10", DiscountType = DiscountType.Percentage, DiscountValue = 10m });

        db.SaveChanges();
        return db;
    }

    private static OrderService CreateService(AppDbContext db)
    {
        return new OrderService(
            new OrderRepository(db),
            new ProductRepository(db),
            new PricingService());
    }

    [Fact]
    public async Task CreateAsync_creates_order_decrements_stock_and_computes_total()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        var request = new CreateOrderRequest
        {
            Items = new()
            {
                new CreateOrderItemRequest { ProductId = 1, Quantity = 2 }, // ノート x2 = 600
                new CreateOrderItemRequest { ProductId = 2, Quantity = 1 }  // マグカップ x1 = 1200
            }
        };

        var result = await service.CreateAsync(request);

        Assert.Equal("Confirmed", result.Status);
        Assert.Equal(1980m, result.TotalAmount); // 1800 * 1.10
        Assert.Equal(2, result.Items.Count);

        // 在庫が引かれている
        Assert.Equal(48, (await db.Products.FindAsync(1))!.Stock);
        Assert.Equal(19, (await db.Products.FindAsync(2))!.Stock);

        // 単価スナップショットと商品名が入っている
        var noteLine = result.Items.Single(i => i.ProductId == 1);
        Assert.Equal("ノート", noteLine.ProductName);
        Assert.Equal(300m, noteLine.UnitPrice);
        Assert.Equal(600m, noteLine.LineTotal);
    }

    [Fact]
    public async Task CreateAsync_succeeds_when_quantity_equals_stock()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        var request = new CreateOrderRequest
        {
            Items = new()
            {
                new CreateOrderItemRequest { ProductId = 1, Quantity = 50 }, // ノートを数量ピッタリ
            }
        };

        var result = await service.CreateAsync(request);

        // 在庫が0になるまで引かれている
        Assert.Equal(0, (await db.Products.FindAsync(1))!.Stock);

        // 注文明細が除外されていない
        Assert.Equal(1, result?.Items.Count);

    }

    [Fact]
    public async Task CreateAsync_applies_coupon()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        var request = new CreateOrderRequest
        {
            Items = new() { new CreateOrderItemRequest { ProductId = 2, Quantity = 1 } }, // 1200
            CouponCode = "WELCOME500"
        };

        var result = await service.CreateAsync(request);

        Assert.Equal("WELCOME500", result.CouponCode);
        Assert.Equal(770m, result.TotalAmount); // (1200 - 500) * 1.10
    }

    [Fact]
    public async Task CreateAsync_throws_when_stock_is_insufficient()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        var request = new CreateOrderRequest
        {
            Items = new() { new CreateOrderItemRequest { ProductId = 3, Quantity = 1 } } // 在庫 0
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(request));

        // 失敗時に在庫が変わっていないこと
        Assert.Equal(0, (await db.Products.FindAsync(3))!.Stock);
    }

    [Fact]
    public async Task CreateAsync_throws_NotFound_when_product_does_not_exist()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        var request = new CreateOrderRequest
        {
            Items = new() { new CreateOrderItemRequest { ProductId = 999, Quantity = 1 } }
        };

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_throws_when_coupon_is_invalid()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        var request = new CreateOrderRequest
        {
            Items = new() { new CreateOrderItemRequest { ProductId = 1, Quantity = 1 } },
            CouponCode = "NOPE"
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task CancelAsync_restores_stock_and_sets_status_cancelled()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        var created = await service.CreateAsync(new CreateOrderRequest
        {
            Items = new() { new CreateOrderItemRequest { ProductId = 1, Quantity = 5 } }
        });
        Assert.Equal(45, (await db.Products.FindAsync(1))!.Stock);

        var cancelled = await service.CancelAsync(created.Id);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(50, (await db.Products.FindAsync(1))!.Stock); // 在庫が戻っている
    }

    [Fact]
    public async Task CancelAsync_throws_when_already_cancelled()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        var created = await service.CreateAsync(new CreateOrderRequest
        {
            Items = new() { new CreateOrderItemRequest { ProductId = 1, Quantity = 1 } }
        });
        await service.CancelAsync(created.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CancelAsync(created.Id));
    }

    [Fact]
    public async Task CancelAsync_throws_NotFound_for_unknown_order()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.CancelAsync(12345));
    }

    [Fact]
    public async Task GetByIdAsync_throws_NotFound_for_unknown_order()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(12345));
    }

    [Fact]
    public async Task GetAllAsync_returns_orders_newest_first()
    {
        using var db = SeededContext();
        var service = CreateService(db);

        var first = await service.CreateAsync(new CreateOrderRequest
        {
            Items = new() { new CreateOrderItemRequest { ProductId = 1, Quantity = 1 } }
        });
        await Task.Delay(50); // DateTime.UtcNow の分解能（約 15ms）を確実に超える
        var second = await service.CreateAsync(new CreateOrderRequest
        {
            Items = new() { new CreateOrderItemRequest { ProductId = 2, Quantity = 1 } }
        });

        var all = await service.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal(second.Id, all[0].Id); // CreatedAt 降順
        Assert.Equal(first.Id, all[1].Id);
    }
}
