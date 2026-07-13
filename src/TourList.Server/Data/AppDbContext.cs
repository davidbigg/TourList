using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TourList.Server.Data.Entities;

namespace TourList.Server.Data;

/// <summary>
/// EF Core context. Identity tables + the TourList domain. Relationship rules here are
/// deliberate and load-bearing (see per-entity remarks) — re-read the implementation plan
/// before changing delete behaviors.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<AddressList> Lists => Set<AddressList>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<SavedRoute> Routes => Set<SavedRoute>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AddressList>(list =>
        {
            list.Property(x => x.Name).HasMaxLength(200).IsRequired();
            list.Property(x => x.RowVersion).IsRowVersion();

            // List → Entry: cascade. This cascade is intentionally confined to entries.
            list.HasMany(x => x.Entries)
                .WithOne(e => e.List!)
                .HasForeignKey(e => e.ListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Entry>(entry =>
        {
            entry.Property(x => x.RawAddress).HasMaxLength(500).IsRequired();
            entry.Property(x => x.FormattedAddress).HasMaxLength(500);
            entry.Property(x => x.Notes).HasMaxLength(2000);
            entry.Property(x => x.GeocodeConfidence).HasMaxLength(32);

            entry.HasIndex(x => x.ListId);
            entry.HasIndex(x => x.GeocodeStatus); // flagged-entry query
        });

        builder.Entity<SavedRoute>(route =>
        {
            route.Property(x => x.Name).HasMaxLength(200).IsRequired();
            route.Property(x => x.StartLabel).HasMaxLength(200);

            // Owner (ApplicationUser) → Route: cascade. Removing a user purges their routes;
            // deactivation does NOT delete the user, so routes are retained.
            route.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Route → RouteStop: cascade.
            route.HasMany(x => x.Stops)
                .WithOne(s => s.Route!)
                .HasForeignKey(s => s.RouteId)
                .OnDelete(DeleteBehavior.Cascade);

            // App-enforced uniqueness for (owner, name); filtered unique index is the DB backstop
            // against the create/merge race (only meaningful for non-empty names).
            route.HasIndex(x => new { x.OwnerUserId, x.Name })
                .IsUnique()
                .HasFilter("[Name] IS NOT NULL");
        });

        builder.Entity<RouteStop>(stop =>
        {
            stop.Property(x => x.SnapshotAddress).HasMaxLength(500).IsRequired();
            stop.Property(x => x.SnapshotNotes).HasMaxLength(2000);
            stop.HasIndex(x => new { x.RouteId, x.Order });

            // Provenance-only FK to Entry: SetNull on entry delete, NO navigation, NO cascade.
            // This is what stops any Entry/List delete from ever reaching a saved route.
            stop.HasOne<Entry>()
                .WithMany()
                .HasForeignKey(x => x.SourceEntryId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
