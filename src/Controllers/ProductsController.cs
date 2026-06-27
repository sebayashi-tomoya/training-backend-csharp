using Microsoft.AspNetCore.Mvc;
using TrainingBackend.Dtos;
using TrainingBackend.Exceptions;
using TrainingBackend.Repositories;

namespace TrainingBackend.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;

    public ProductsController(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    /// <summary>商品一覧を取得する</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
    {
        var products = await _productRepository.GetAllAsync();
        var dtos = products.Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Stock));
        return Ok(dtos);
    }

    /// <summary>商品詳細を取得する（存在しない場合は 404）</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetById(int id)
    {
        var product = await _productRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"商品が見つかりません (ProductId: {id})");

        return Ok(new ProductDto(product.Id, product.Name, product.Price, product.Stock));
    }

    /// <summary>商品の価格を変更する（存在しない場合は 404）</summary>
    /// <remarks>
    /// 既存の確定済み注文には影響しない。注文明細は注文時点の単価（OrderItem.UnitPrice）を
    /// スナップショットとして保持しているため、ここで変えた価格は新規注文にのみ反映される。
    /// </remarks>
    [HttpPut("{id:int}/price")]
    public async Task<ActionResult<ProductDto>> UpdatePrice(int id, [FromBody] UpdateProductPriceRequest request)
    {
        var product = await _productRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"商品が見つかりません (ProductId: {id})");

        product.Price = request.Price;
        await _productRepository.SaveChangesAsync();

        return Ok(new ProductDto(product.Id, product.Name, product.Price, product.Stock));
    }
}
