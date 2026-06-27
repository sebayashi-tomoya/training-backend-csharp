using TrainingBackend.Entities;

namespace TrainingBackend.Repositories;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync();

    Task<Product?> GetByIdAsync(int id);

    /// <summary>指定 Id の商品をまとめて取得する（注文作成時に複数商品を引くため）</summary>
    Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids);

    /// <summary>商品の変更を永続化する</summary>
    Task SaveChangesAsync();
}
