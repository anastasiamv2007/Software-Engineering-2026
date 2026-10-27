using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public class UserRepository : IUserRepository {
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public UserRepository(IDbContextFactory<AppDbContext> contextFactory) {
        _contextFactory = contextFactory;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) {
        string normalizedEmail = email.Trim().ToLowerInvariant();
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) {
        string normalizedEmail = email.Trim().ToLowerInvariant();
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
