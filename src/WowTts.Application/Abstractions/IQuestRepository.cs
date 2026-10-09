using WowTts.Domain.Quests;

namespace WowTts.Application.Abstractions;

// Where quests are kept. Infrastructure stores them in a SQLite database.
public interface IQuestRepository
{
    Task<IReadOnlyList<Quest>> GetAllAsync();

    // Returns null when there's no quest with that ID.
    Task<Quest?> GetByIdAsync(int id);

    // Adds new quests and replaces existing ones with the same ID.
    Task SaveAsync(IReadOnlyCollection<Quest> quests);
}
