using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WowTts.Infrastructure.Persistence;

public static class DatabaseSetup
{
    // Columns added after databases already existed. EnsureCreated doesn't add columns to an existing database,
    // so they're added here, which keeps the quests, cast voices and performances it already holds.
    private static readonly (string Table, string Column, string Type)[] AddedColumns =
    [
        ("Quests", "EnderName", "TEXT"),
        ("Quests", "EnderRace", "TEXT"),
        ("Quests", "EnderGender", "TEXT"),
        ("AudioPerformances", "Engine", "TEXT"),
        ("AudioPerformances", "Instructions", "TEXT"),
    ];

    // Tables added after databases already existed, created from the model the same way EnsureCreated would.
    private static readonly string[] AddedTables = ["AudioTimings", "NpcCharacters"];

    // Creates the database file and its tables if they don't exist yet. Called once when an app starts.
    public static async Task EnsureDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WowTtsDbContext>();

        string file = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        await db.Database.EnsureCreatedAsync();
        await AddMissingColumnsAsync(db);
        await AddMissingTablesAsync(db);

        // EnsureCreated doesn't change an existing database. If it was made by an older version of the app,
        // the newer tables or columns are missing, so say how to fix it instead of failing later.
        try
        {
            await db.Quests.OrderBy(q => q.Id).Select(q => new { q.GiverX, q.CategoryId, q.Side, q.EnderName }).Take(1).ToListAsync();
            await db.MapZones.AnyAsync();
            await db.QuestCategories.AnyAsync();
            await db.NpcVoices.AnyAsync();
            await db.AudioPerformances.AnyAsync();
            await db.AudioTimings.AnyAsync();
            await db.NpcCharacters.AnyAsync();
            await db.AudioPerformances.OrderBy(p => p.QuestId).Select(p => new { p.Engine, p.Instructions }).Take(1).ToListAsync();
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException(
                $"The database {file} was created by an older version of wow-tts ({ex.Message}). " +
                "Delete it and run the importer again: gamedata, then cmangos.", ex);
        }
    }

    private static async Task AddMissingTablesAsync(WowTtsDbContext db)
    {
        // EF Core's own CREATE script, split into statements; only those for missing tables are run.
        string[] statements = db.Database.GenerateCreateScript()
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string table in AddedTables)
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var exists = connection.CreateCommand();
            exists.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'";
            if (Convert.ToInt64(await exists.ExecuteScalarAsync()) > 0)
                continue;

            foreach (string statement in statements.Where(s => s.Contains($"\"{table}\"")))
                await db.Database.ExecuteSqlRawAsync(statement);
        }
    }

    private static async Task AddMissingColumnsAsync(WowTtsDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        foreach (var (table, column, type) in AddedColumns)
        {
            using var check = connection.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
            bool tableHasColumn = Convert.ToInt64(await check.ExecuteScalarAsync()) > 0;

            using var tableExists = connection.CreateCommand();
            tableExists.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'";
            if (tableHasColumn || Convert.ToInt64(await tableExists.ExecuteScalarAsync()) == 0)
                continue;

            using var add = connection.CreateCommand();
            add.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type} NULL";
            await add.ExecuteNonQueryAsync();
        }
    }
}
