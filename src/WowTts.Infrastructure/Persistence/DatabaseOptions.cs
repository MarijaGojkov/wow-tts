namespace WowTts.Infrastructure.Persistence;

// The "Database" section of appsettings.json. A relative path is relative to the app's project folder,
// so the Api and the importer both point at the same data/wowtts.db.
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string File { get; set; } = "../../data/wowtts.db";
}
