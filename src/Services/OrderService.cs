using TrainingBackend.Dtos;
using TrainingBackend.Entities;
using TrainingBackend.Exceptions;
using TrainingBackend.Repositories;

namespace TrainingBackend.Services;

/// <summary>
/// 注文に関する業務ロジックの中心
/// 在庫チェック・クーポン適用・合計金額の確定・在庫の戻し（キャンセル）を担う
/// </summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IPricingService _pricingService;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IPricingService pricingService)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _pricingService = pricingService;
    }

    public async Task<IReadOnlyList<OrderDto>> GetAllAsync()
    {
        var orders = await _orderRepository.GetAllAsync();
        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderDto> GetByIdAsync(int id)
    {
        var order = await _orderRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"注文が見つかりません (OrderId: {id})");

        return MapToDto(order);
    }

    public async Task<OrderDto> CreateAsync(CreateOrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            throw new BusinessRuleException("注文には商品を 1 つ以上含めてください。");
        }

        // 同じ商品が複数行で来ても在庫チェックが正しく効くよう、商品ごとに数量を合算する
        var requestedQuantities = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        if (requestedQuantities.Values.Any(q => q <= 0))
        {
            throw new BusinessRuleException("数量は 1 以上で指定してください。");
        }

        var products = await _productRepository.GetByIdsAsync(requestedQuantities.Keys);
        var productById = products.ToDictionary(p => p.Id);

        var coupon = await ResolveCouponAsync(request.CouponCode);

        var order = new Order
        {
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            CouponId = coupon?.Id
        };

        foreach (var (productId, quantity) in requestedQuantities)
        {
            if (!productById.TryGetValue(productId, out var product))
            {
                throw new NotFoundException($"商品が見つかりません (ProductId: {productId})");
            }

            if (product.Stock < quantity)
            {
                throw new BusinessRuleException(
                    $"在庫が不足しています (商品: {product.Name}, 在庫: {product.Stock}, 要求: {quantity})");
            }

            product.Stock -= quantity;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                UnitPrice = product.Price // 注文時点の単価をスナップショットとして保存
            });
        }

        order.TotalAmount = _pricingService.CalculateTotal(order.Items, coupon);

        await _orderRepository.AddAsync(order);
        await _orderRepository.SaveChangesAsync();

        // 商品名・クーポン込みの完全なグラフを読み直して返す
        var created = await _orderRepository.GetByIdAsync(order.Id);
        return MapToDto(created!);
    }

    public async Task<OrderDto> CancelAsync(int id)
    {
        var order = await _orderRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"注文が見つかりません (OrderId: {id})");

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new BusinessRuleException("この注文は既にキャンセル済みです。");
        }

        // 在庫を戻す
        foreach (var item in order.Items)
        {
            item.Product.Stock += item.Quantity;
        }

        order.Status = OrderStatus.Cancelled;
        await _orderRepository.SaveChangesAsync();

        return MapToDto(order);
    }

    private async Task<Coupon?> ResolveCouponAsync(string? couponCode)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return null;
        }

        return await _orderRepository.GetCouponByCodeAsync(couponCode)
            ?? throw new BusinessRuleException($"クーポンが無効です (Code: {couponCode})");
    }

    private static OrderDto MapToDto(Order order)
    {
        var items = order.Items
            .Select(i => new OrderItemDto(
                i.ProductId,
                i.Product?.Name ?? string.Empty,
                i.Quantity,
                i.Product?.Price ?? 0m,
                (i.Product?.Price ?? 0m) * i.Quantity))
            .ToList();

        return new OrderDto(
            order.Id,
            order.Status.ToString(),
            order.TotalAmount,
            order.Coupon?.Code,
            order.CreatedAt,
            items);
    }
}
