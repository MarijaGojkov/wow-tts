using System.Globalization;
using WowTts.Domain.Maps;
using WowTts.Domain.Quests;

namespace WowTts.Infrastructure.Import;

// Quests read from a dump, plus the ones that were left out and why.
public sealed record QuestReadResult(IReadOnlyList<Quest> Quests, IReadOnlyDictionary<string, int> Skipped);

// Turns the cMaNGOS classic-db dump (https://github.com/cmangos/classic-db) into quests.
// Accept text is Details, progress text is RequestItemsText, complete text is OfferRewardText.
// The quest giver is the first NPC (or object, e.g. a wanted poster) that starts the quest,
// and its position is that NPC's or object's first spawn in the world.
public static class CMangosQuestReader
{
    private const string Unknown = "Unknown";

    // RequiredRaces is a bit mask of playable races.
    private const int AllianceRaces = 1 | 4 | 8 | 64;    // Human, Dwarf, Night Elf, Gnome
    private const int HordeRaces = 2 | 16 | 32 | 128;    // Orc, Undead, Tauren, Troll

    // Titles of quests that were never used in the game, or are test and placeholder quests.
    private static readonly string[] UnusedTitleMarkers =
        ["UNUSED", "DEPRECATED", "<NYI>", "<TXT>", "<TEST>", "[PH]", "<CHANGE TO GOSSIP>", "ZZOLD", "<NOT IN USE>"];

    private sealed record Giver(string Name, string Gender, WorldLocation? Location);

    public static QuestReadResult Read(string dumpPath)
    {
        var tables = MySqlDumpReader.ReadTables(dumpPath, new Dictionary<string, string[]>
        {
            ["quest_template"] = ["entry", "Title", "Details", "RequestItemsText", "OfferRewardText",
                                  "ZoneOrSort", "QuestLevel", "MinLevel", "RequiredRaces"],
            ["creature_questrelation"] = ["id", "quest"],
            ["gameobject_questrelation"] = ["id", "quest"],
            ["creature_template"] = ["Entry", "Name", "ModelId1"],
            ["creature_model_info"] = ["modelid", "gender"],
            ["gameobject_template"] = ["entry", "name"],
            ["creature"] = ["guid", "id", "map", "position_x", "position_y"],
            ["gameobject"] = ["guid", "id", "map", "position_x", "position_y"],
        });

        // creature_model_info.gender: 0 = male, 1 = female, 2 = none.
        var genderByModel = new Dictionary<int, string>();
        foreach (var row in tables["creature_model_info"])
            genderByModel[ToInt(row[0])] = row[1] switch { "0" => "Male", "1" => "Female", _ => Unknown };

        var npcSpawns = FirstSpawns(tables["creature"]);
        var objectSpawns = FirstSpawns(tables["gameobject"]);

        var npcs = new Dictionary<int, Giver>();
        foreach (var row in tables["creature_template"])
        {
            int entry = ToInt(row[0]);
            npcs[entry] = new Giver(
                row[1] ?? Unknown,
                genderByModel.GetValueOrDefault(ToInt(row[2]), Unknown),
                npcSpawns.TryGetValue(entry, out var spawn) ? spawn : null);
        }

        var objects = new Dictionary<int, Giver>();
        foreach (var row in tables["gameobject_template"])
        {
            int entry = ToInt(row[0]);
            objects[entry] = new Giver(
                row[1] ?? Unknown,
                Unknown,
                objectSpawns.TryGetValue(entry, out var spawn) ? spawn : null);
        }

        // Several NPCs can start the same quest. Use the one with the lowest ID, so the result is stable.
        var giverByQuest = new Dictionary<int, Giver>();
        foreach (var row in tables["creature_questrelation"].OrderBy(row => ToInt(row[0])))
        {
            int quest = ToInt(row[1]);
            if (!giverByQuest.ContainsKey(quest) && npcs.TryGetValue(ToInt(row[0]), out var npc))
                giverByQuest[quest] = npc;
        }
        foreach (var row in tables["gameobject_questrelation"].OrderBy(row => ToInt(row[0])))
        {
            int quest = ToInt(row[1]);
            if (!giverByQuest.ContainsKey(quest) && objects.TryGetValue(ToInt(row[0]), out var gameObject))
                giverByQuest[quest] = gameObject;
        }

        var quests = new List<Quest>();
        var skipped = new Dictionary<string, int>();
        var noGiver = new Giver(Unknown, Unknown, null);

        foreach (var row in tables["quest_template"])
        {
            int id = ToInt(row[0]);
            string title = (row[1] ?? "").Trim();
            string? acceptText = EmptyToNull(row[2]);
            string? progressText = EmptyToNull(row[3]);
            string? completeText = EmptyToNull(row[4]);

            string? reason =
                title.Length == 0 ? "no title" :
                UnusedTitleMarkers.Any(marker => title.Contains(marker, StringComparison.OrdinalIgnoreCase)) ? "unused or test quest" :
                acceptText is null && progressText is null && completeText is null ? "no text at all" :
                null;

            if (reason is not null)
            {
                skipped[reason] = skipped.GetValueOrDefault(reason) + 1;
                continue;
            }

            Giver giver = giverByQuest.GetValueOrDefault(id, noGiver);
            int zoneOrSort = ToInt(row[5]);
            int questLevel = ToInt(row[6]);
            int minLevel = ToInt(row[7]);

            quests.Add(new Quest(
                id,
                title,
                giver.Name,
                npcRace: Unknown,   // cMaNGOS doesn't store an NPC's race
                giver.Gender,
                acceptText,
                progressText,
                completeText,
                QuestSource.CMangos,
                level: questLevel > 0 ? questLevel : null,   // -1 means the quest scales with your level
                minLevel: minLevel > 0 ? minLevel : null,
                side: SideFromRaces(ToInt(row[8])),
                categoryId: zoneOrSort != 0 ? zoneOrSort : null,
                giverLocation: giver.Location));
        }

        return new QuestReadResult(quests, skipped);
    }

    // The first spawn (lowest guid) of each NPC or object: its map and world position.
    private static Dictionary<int, WorldLocation> FirstSpawns(List<string?[]> spawns)
    {
        var result = new Dictionary<int, WorldLocation>();
        foreach (var row in spawns.OrderBy(row => ToInt(row[0])))
        {
            int entry = ToInt(row[1]);
            if (!result.ContainsKey(entry))
                result[entry] = new WorldLocation(ToInt(row[2]), ToDouble(row[3]), ToDouble(row[4]));
        }
        return result;
    }

    private static QuestSide SideFromRaces(int races)
    {
        bool alliance = (races & AllianceRaces) != 0;
        bool horde = (races & HordeRaces) != 0;
        return alliance && !horde ? QuestSide.Alliance
             : horde && !alliance ? QuestSide.Horde
             : QuestSide.Both;
    }

    private static int ToInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : 0;

    private static double ToDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : 0;

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
