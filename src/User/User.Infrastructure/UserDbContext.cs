using Microsoft.EntityFrameworkCore;
using User.Domain.Entities;

namespace User.Infrastructure;

public sealed class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserDbContext).Assembly);
}
