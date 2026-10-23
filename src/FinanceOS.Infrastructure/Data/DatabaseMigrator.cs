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
        await db.Database.EnsureCreatedAsync(ct);

        // 2. Safely add missing columns to existing tables
        var addColumnsSql = """
            ALTER TABLE "Owners" ADD COLUMN IF NOT EXISTS "BaseCurrency" integer NOT NULL DEFAULT 0;
            ALTER TABLE "Owners" ADD COLUMN IF NOT EXISTS "Archetype" text NULL;
            ALTER TABLE "Owners" ADD COLUMN IF NOT EXISTS "OnboardingCompleted" boolean NOT NULL DEFAULT true;
            ALTER TABLE "Owners" ADD COLUMN IF NOT EXISTS "ActiveWorkspaceId" uuid NULL;
            ALTER TABLE "LedgerTransactions" ADD COLUMN IF NOT EXISTS "Merchant" text NULL;
            ALTER TABLE "BalanceSnapshots" ADD COLUMN IF NOT EXISTS "Source" text NOT NULL DEFAULT 'manual';
            """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(addColumnsSql, ct);
        }
        catch
        {
            // Ignore if column alter errors
        }

        // 3. Ensure Household collaboration tables exist
        var createHouseholdSql = """
            CREATE TABLE IF NOT EXISTS "HouseholdWorkspaces" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "PrimaryOwnerId" uuid NOT NULL,
                "Name" text NOT NULL,
                "InviteToken" text NULL,
                "InviteExpiresAtUtc" timestamp with time zone NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_HouseholdWorkspaces_InviteToken" ON "HouseholdWorkspaces" ("InviteToken");

            CREATE TABLE IF NOT EXISTS "HouseholdMembers" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "WorkspaceId" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "Role" text NOT NULL,
                "JoinedAtUtc" timestamp with time zone NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_HouseholdMembers_WorkspaceId_UserId" ON "HouseholdMembers" ("WorkspaceId", "UserId");
            """;

        try
        {
            await db.Database.ExecuteSqlRawAsync(createHouseholdSql, ct);
        }
        catch
        {
            // Ignore if table creation errors
        }
    }

    private static async Task MigrateSqliteAsync(FinanceDbContext db, CancellationToken ct)
    {
        await db.Database.EnsureCreatedAsync(ct);

        // SQLite column additions
        string[] columns =
        [
            "ALTER TABLE Owners ADD COLUMN BaseCurrency INTEGER NOT NULL DEFAULT 0;",
            "ALTER TABLE Owners ADD COLUMN Archetype TEXT NULL;",
            "ALTER TABLE Owners ADD COLUMN OnboardingCompleted INTEGER NOT NULL DEFAULT 1;",
            "ALTER TABLE Owners ADD COLUMN ActiveWorkspaceId TEXT NULL;",
            "ALTER TABLE LedgerTransactions ADD COLUMN Merchant TEXT NULL;",
            "ALTER TABLE BalanceSnapshots ADD COLUMN Source TEXT NOT NULL DEFAULT 'manual';"
        ];

        foreach (var col in columns)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(col, ct);
            }
            catch
            {
                // SQLite throws if column already exists
            }
        }
    }
}
