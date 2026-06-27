using System.ComponentModel.DataAnnotations;

namespace TrainingBackend.Dtos;

/// <summary>商品価格の変更リクエスト</summary>
public class UpdateProductPriceRequest
{
    [Range(0, double.MaxValue, ErrorMessage = "価格は 0 以上で指定してください。")]
    public decimal Price { get; set; }
}
