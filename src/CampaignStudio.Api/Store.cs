using CampaignStudio.Core;
using Microsoft.EntityFrameworkCore;

namespace CampaignStudio.Api;

public class Store(DbContextOptions options) : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<RecipientJob> Jobs => Set<RecipientJob>();
    public DbSet<SenderProfile> Senders => Set<SenderProfile>();
    public DbSet<SendAttempt> Attempts => Set<SendAttempt>();
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<Exclusion> Exclusions => Set<Exclusion>();
    public DbSet<ConsentEvent> ConsentEvents => Set<ConsentEvent>();
    public DbSet<LibraryItem> Library => Set<LibraryItem>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Contact>().HasIndex(x => x.Email).IsUnique();
        b.Entity<RecipientJob>().HasIndex(x => new { x.CampaignId, x.ContactId }).IsUnique();
        b.Entity<RecipientJob>().HasIndex(x => new { x.State, x.StartedAt });
        b.Entity<Contact>().HasIndex(x => new { x.ListName, x.Suppressed });
        b.Entity<Campaign>().HasIndex(x => new { x.State, x.ScheduledAt });
        b.Entity<RecipientJob>().HasIndex(x => new { x.CampaignId, x.State, x.NextAttemptAt });
        b.Entity<SendAttempt>().HasIndex(x => new { x.JobId, x.Number }).IsUnique();
        b.Entity<ConsentEvent>().HasIndex(x => new { x.ContactId, x.At });
        b.Entity<Exclusion>().HasIndex(x => new { x.Kind, x.Value }).IsUnique();
    }
}

public sealed class SqliteStore(DbContextOptions<SqliteStore> options) : Store(options);
public sealed class PostgresStore(DbContextOptions<PostgresStore> options) : Store(options);
