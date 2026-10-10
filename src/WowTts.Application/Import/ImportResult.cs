namespace WowTts.Application.Import;

// What an import did.
// KeptExisting: quests skipped because the database already has text from a higher-priority source.
// AudioDeleted: MP3s removed because their quest text, or who speaks it, changed.
public sealed record ImportResult(
    int Read,
    int Added,
    int Updated,
    int Unchanged,
    int KeptExisting,
    int AudioDeleted);
