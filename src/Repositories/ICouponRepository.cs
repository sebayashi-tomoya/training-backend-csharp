using TrainingBackend.Entities;

namespace TrainingBackend.Repositories;

public interface ICouponRepository
{
    Task<List<Coupon>> GetAllAsync();
}
