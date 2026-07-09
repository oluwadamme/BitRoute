using BitRoute.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BitRoute.Infrastructure.Persistence;

/// <summary>
/// The EF Core database context. It derives from <see cref="IdentityDbContext{TUser,TRole,TKey}"/>
/// to get the ASP.NET Identity tables (users, roles, user-roles, claims, tokens) with a Guid key.
/// Domain tables (schedules, bookings) are added in Phase 3 via entity configurations picked up
/// from this assembly.
/// </summary>
public sealed class BitRouteDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public BitRouteDbContext(DbContextOptions<BitRouteDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(BitRouteDbContext).Assembly);
    }
}
