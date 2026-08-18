using Jobs.Domain.Common;

namespace Jobs.Domain.Jobs;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(JobId id, CancellationToken cancellationToken = default);
    Task AddAsync(Job job, CancellationToken cancellationToken = default);
}
