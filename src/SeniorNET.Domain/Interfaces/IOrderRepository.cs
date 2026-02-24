using SeniorNET.Domain.Entities;

namespace SeniorNET.Domain.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(string customerId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
