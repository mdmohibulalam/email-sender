using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;

namespace CampaignStudio.Api;

public sealed class Store(DbContextOptions<Store> options) : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<RecipientJob> Jobs => Set<RecipientJob>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Contact>().HasIndex(x => x.Email).IsUnique();
        b.Entity<RecipientJob>().HasIndex(x => new { x.CampaignId, x.ContactId }).IsUnique();
        b.Entity<RecipientJob>().HasIndex(x => new { x.State, x.StartedAt });
    }
}
