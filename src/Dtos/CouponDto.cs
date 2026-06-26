namespace TrainingBackend.Dtos;

/// <summary>クーポンをクライアントに返すための DTO</summary>
public record CouponDto(
    string Code,
    string DiscountType,
    decimal DiscountValue);
