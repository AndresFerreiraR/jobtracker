using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence.Repositories;

internal sealed class JobRepository(JobsDbContext db) : IJobRepository
{
    public Task<Job?> GetByIdAsync(JobId id, CancellationToken cancellationToken = default) =>
        db.Jobs
            .Include(j => j.Photos)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task AddAsync(Job job, CancellationToken cancellationToken = default) =>
        await db.Jobs.AddAsync(job, cancellationToken);
}
