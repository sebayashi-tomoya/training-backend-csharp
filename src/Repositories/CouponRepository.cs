using Microsoft.EntityFrameworkCore;
using TrainingBackend.Data;
using TrainingBackend.Entities;

namespace TrainingBackend.Repositories;

public class CouponRepository : ICouponRepository
{
    private readonly AppDbContext _db;

    public CouponRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Coupon>> GetAllAsync()
    {
        return await _db.Coupons
            .OrderBy(c => c.Id)
            .ToListAsync();
    }
}
