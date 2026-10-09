using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WowTts.Infrastructure.Persistence;

public static class DatabaseSetup
{
    // Creates the database file and its tables if they don't exist yet. Called once when an app starts.
    public static async Task EnsureDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WowTtsDbContext>();

        string file = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        await db.Database.EnsureCreatedAsync();

        // EnsureCreated doesn't change an existing database. If it was made by an older version of the app,
        // the newer tables or columns are missing, so say how to fix it instead of failing later.
        try
        {
            await db.Quests.Select(q => new { q.GiverX, q.CategoryId, q.Side }).Take(1).ToListAsync();
            await db.MapZones.AnyAsync();
            await db.QuestCategories.AnyAsync();
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException(
                $"The database {file} was created by an older version of wow-tts ({ex.Message}). " +
                "Delete it and run the importer again: gamedata, then cmangos.", ex);
        }
    }
}
