using TrainingBackend.Entities;

namespace TrainingBackend.Services;

/// <summary>
/// 金額計算を担当する（小計・クーポン割引・税込合計の 3 ステップ）
/// 計算の流れ: 税抜小計 → クーポン割引 → 消費税を乗せて円未満四捨五入
/// </summary>
public class PricingService : IPricingService
{
    /// <summary>消費税率（10%）</summary>
    public const decimal TaxRate = 0.10m;

    public decimal CalculateSubtotal(IEnumerable<OrderItem> items)
    {
        return items.Sum(i => i.UnitPrice * i.Quantity);
    }

    public decimal ApplyCoupon(decimal subtotal, Coupon? coupon)
    {
        if (coupon is null)
        {
            return subtotal;
        }

        var discounted = coupon.DiscountType switch
        {
            DiscountType.FixedAmount => subtotal - coupon.DiscountValue,
            DiscountType.Percentage => subtotal * (1m - coupon.DiscountValue / 100m),
            _ => subtotal
        };

        return discounted;
    }

    public decimal CalculateTotal(IEnumerable<OrderItem> items, Coupon? coupon)
    {
        var subtotal = CalculateSubtotal(items);
        var discounted = ApplyCoupon(subtotal, coupon);
        var withTax = discounted * (1m + TaxRate);

        // 円未満は四捨五入（0.5 は切り上げ）
        return Math.Round(withTax, 0, MidpointRounding.AwayFromZero);
    }
}
