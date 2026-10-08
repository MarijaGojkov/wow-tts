using WowTts.Domain.Quests;

namespace WowTts.Application.Abstractions;

// Where quests come from. Infrastructure reads them from a JSON file for now.
public interface IQuestRepository
{
    Task<IReadOnlyList<Quest>> GetAllAsync();

    // Returns null when there's no quest with that ID.
    Task<Quest?> GetByIdAsync(int id);
}
