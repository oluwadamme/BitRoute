using BitRoute.Domain.Entities;

namespace BitRoute.Domain.Interfaces;

public interface IScheduleRepository
{
    Task<Schedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Schedule>> GetAllAsync(CancellationToken cancellationToken = default);
    
    void Add(Schedule schedule);
}
