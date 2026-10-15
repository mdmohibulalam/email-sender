using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace CampaignStudio.Api;

public sealed class WorkspaceLease : IDisposable
{
    private readonly FileStream stream;
    public WorkspaceLease(StudioSettings settings) => stream = new FileStream(Path.Combine(settings.DataDirectory, "workspace.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    public void Dispose() => stream.Dispose();
}
public static class SchemaUpgrade
{
    private sealed record Snapshot(string Path, string Sha256);
    public static void Apply(Store db, StudioSettings settings)
    {
        db.Database.OpenConnection();
        var connection = db.Database.GetDbConnection();
        string marker = Path.Combine(settings.DataDirectory, "schema-upgrade.json");
        string backups = Path.GetFullPath(Path.Combine(settings.DataDirectory, "backups"));
        string Hash(string path) { using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return Convert.ToHexString(SHA256.HashData(f)); }
        if (connection is SqliteConnection sqlite && File.Exists(marker))
        {
            var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(marker)) ?? throw new InvalidOperationException("Invalid upgrade recovery marker.");
            if (Path.GetDirectoryName(Path.GetFullPath(snapshot.Path)) != backups || Hash(snapshot.Path) != snapshot.Sha256) throw new InvalidOperationException("Upgrade backup verification failed.");
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshot.Path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            source.Open(); source.BackupDatabase(sqlite); File.Delete(marker);
        }
        bool HasTable(string table)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = connection is SqliteConnection ? "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@name" : "SELECT count(*) FROM information_schema.tables WHERE table_schema=current_schema() AND table_name=@name";
            var p = cmd.CreateParameter(); p.ParameterName = "name"; p.Value = table; cmd.Parameters.Add(p);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
        bool legacy = HasTable("Contacts") && !HasTable("__EFMigrationsHistory");
        if (legacy)
        {
            var expected = new Dictionary<string, string[]>
            {
                ["Contacts"] = ["Id", "Email", "FirstName", "LastName", "Company", "ListName", "ConsentSource", "CreatedAt", "Suppressed", "SuppressionReason"],
                ["Campaigns"] = ["Id", "Name", "Subject", "Preheader", "Heading", "Body", "ButtonLabel", "ButtonUrl", "Footer", "Accent", "ListName", "State", "Mode", "ScheduledAt", "CreatedAt"],
                ["Jobs"] = ["Id", "CampaignId", "ContactId", "Email", "FirstName", "LastName", "State", "MessageId", "StartedAt", "FinishedAt", "Detail"]
            };
            foreach (var (table, columns) in expected)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = connection is SqliteConnection ? $"SELECT name FROM pragma_table_info('{table}')" : $"SELECT column_name FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='{table}'";
                using var reader = cmd.ExecuteReader(); var actual = new HashSet<string>(); while (reader.Read()) actual.Add(reader.GetString(0));
                if (!actual.SetEquals(columns)) throw new InvalidOperationException("Unknown database schema. Restore a verified backup before upgrading.");
            }
        }
        if (connection is SqliteConnection live && (legacy || db.Database.GetPendingMigrations().Any()) && HasTable("Contacts"))
        {
            Directory.CreateDirectory(backups); string backup = Path.Combine(backups, "before-0.2-" + Guid.NewGuid() + ".db");
            using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backup, Pooling = false }.ToString()); target.Open(); live.BackupDatabase(target); target.Close();
            File.WriteAllText(marker + ".tmp", JsonSerializer.Serialize(new Snapshot(backup, Hash(backup))));
            using (var f = new FileStream(marker + ".tmp", FileMode.Open, FileAccess.ReadWrite)) f.Flush(true);
            File.Move(marker + ".tmp", marker, true);
        }
        if (legacy)
        {
            var history = db.GetService<IHistoryRepository>();
            using var tx = db.Database.BeginTransaction();
            db.Database.ExecuteSqlRaw(history.GetCreateIfNotExistsScript());
            db.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow(db.Database.GetMigrations().First(), "10.0.13")));
            tx.Commit();
        }
        db.Database.Migrate();
        if (File.Exists(marker)) File.Delete(marker);
    }
}
