using Microsoft.EntityFrameworkCore;

namespace FinanceOS.Infrastructure.Data;

public static class DatabaseMigrator
{
    public static async Task EnsureSchemaUpToDateAsync(FinanceDbContext db, CancellationToken ct = default)
    {
        var provider = db.Database.ProviderName ?? "";

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await MigratePostgresAsync(db, ct);
        }
        else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await MigrateSqliteAsync(db, ct);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }
    }

    private static async Task MigratePostgresAsync(FinanceDbContext db, CancellationToken ct)
    {
        // 1. Ensure all base tables exist first if this is a blank DB
        try
        {
            await db.Database.EnsureCreatedAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Migrator] EnsureCreated notice: {ex.Message}");
        }

        // 2. Safely add missing columns to existing tables individually
        string[] statements =
        [
            "ALTER TABLE \"Owners\" ADD COLUMN IF NOT EXISTS \"BaseCurrency\" integer NOT NULL DEFAULT 0;",
            "ALTER TABLE \"Owners\" ADD COLUMN IF NOT EXISTS \"Archetype\" text NULL;",
            "ALTER TABLE \"Owners\" ADD COLUMN IF NOT EXISTS \"OnboardingCompleted\" boolean NOT NULL DEFAULT true;",
            "ALTER TABLE \"Owners\" ADD COLUMN IF NOT EXISTS \"ActiveWorkspaceId\" uuid NULL;",
            "ALTER TABLE \"Transactions\" ADD COLUMN IF NOT EXISTS \"Merchant\" text NULL;",
            "ALTER TABLE \"BalanceSnapshots\" ADD COLUMN IF NOT EXISTS \"Source\" text NOT NULL DEFAULT 'manual';",
            """
            CREATE TABLE IF NOT EXISTS "HouseholdWorkspaces" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "PrimaryOwnerId" uuid NOT NULL,
                "Name" text NOT NULL,
                "InviteToken" text NULL,
                "InviteExpiresAtUtc" timestamp with time zone NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_HouseholdWorkspaces_InviteToken\" ON \"HouseholdWorkspaces\" (\"InviteToken\");",
            """
            CREATE TABLE IF NOT EXISTS "HouseholdMembers" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "WorkspaceId" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "Role" text NOT NULL,
                "JoinedAtUtc" timestamp with time zone NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_HouseholdMembers_WorkspaceId_UserId\" ON \"HouseholdMembers\" (\"WorkspaceId\", \"UserId\");"
        ];

        foreach (var sql in statements)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Migrator Notice] Postgres statement execution: {ex.Message}");
            }
        }
    }

    private static async Task MigrateSqliteAsync(FinanceDbContext db, CancellationToken ct)
    {
        try
        {
            await db.Database.EnsureCreatedAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Migrator] SQLite EnsureCreated notice: {ex.Message}");
        }

        // SQLite column and table additions
        string[] statements =
        [
            "ALTER TABLE Owners ADD COLUMN BaseCurrency INTEGER NOT NULL DEFAULT 0;",
            "ALTER TABLE Owners ADD COLUMN Archetype TEXT NULL;",
            "ALTER TABLE Owners ADD COLUMN OnboardingCompleted INTEGER NOT NULL DEFAULT 1;",
            "ALTER TABLE Owners ADD COLUMN ActiveWorkspaceId TEXT NULL;",
            "ALTER TABLE Transactions ADD COLUMN Merchant TEXT NULL;",
            "ALTER TABLE BalanceSnapshots ADD COLUMN Source TEXT NOT NULL DEFAULT 'manual';",
            """
            CREATE TABLE IF NOT EXISTS HouseholdWorkspaces (
                Id TEXT NOT NULL PRIMARY KEY,
                PrimaryOwnerId TEXT NOT NULL,
                Name TEXT NOT NULL,
                InviteToken TEXT NULL,
                InviteExpiresAtUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS IX_HouseholdWorkspaces_InviteToken ON HouseholdWorkspaces (InviteToken);",
            """
            CREATE TABLE IF NOT EXISTS HouseholdMembers (
                Id TEXT NOT NULL PRIMARY KEY,
                WorkspaceId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                Role TEXT NOT NULL,
                JoinedAtUtc TEXT NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_HouseholdMembers_WorkspaceId_UserId ON HouseholdMembers (WorkspaceId, UserId);"
        ];

        foreach (var sql in statements)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch
            {
                // SQLite throws if column/table already exists
            }
        }
    }
}
