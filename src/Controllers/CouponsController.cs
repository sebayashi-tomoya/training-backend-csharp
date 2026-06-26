using Microsoft.AspNetCore.Mvc;
using TrainingBackend.Dtos;
using TrainingBackend.Repositories;

namespace TrainingBackend.Controllers;

[ApiController]
[Route("api/coupons")]
public class CouponsController : ControllerBase
{
    private readonly ICouponRepository _couponRepository;

    public CouponsController(ICouponRepository couponRepository)
    {
        _couponRepository = couponRepository;
    }

    /// <summary>利用できるクーポンの一覧を取得する</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CouponDto>>> GetAll()
    {
        var coupons = await _couponRepository.GetAllAsync();
        var dtos = coupons.Select(c => new CouponDto(c.Code, c.DiscountType.ToString(), c.DiscountValue));
        return Ok(dtos);
    }
}
