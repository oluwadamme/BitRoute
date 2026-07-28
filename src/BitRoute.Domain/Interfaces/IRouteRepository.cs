using BitRoute.Domain.Entities;

namespace BitRoute.Domain.Interfaces;

public interface IRouteRepository
{
    Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    
    void Add(Route route);
}
