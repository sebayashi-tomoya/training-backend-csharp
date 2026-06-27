using Microsoft.EntityFrameworkCore;
using TrainingBackend.Data;
using TrainingBackend.Entities;

namespace TrainingBackend.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _db.Products
            .OrderBy(p => p.Id)
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _db.Products.FindAsync(id);
    }

    public async Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids)
    {
        var idSet = ids.Distinct().ToList();
        return await _db.Products
            .Where(p => idSet.Contains(p.Id))
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
