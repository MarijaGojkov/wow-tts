using System.Globalization;
using WowTts.Domain.Maps;
using WowTts.Domain.Quests;

namespace WowTts.Infrastructure.Import;

// Quests read from a dump, plus the ones that were left out and why.
public sealed record QuestReadResult(IReadOnlyList<Quest> Quests, IReadOnlyDictionary<string, int> Skipped);

// Turns the cMaNGOS classic-db dump (https://github.com/cmangos/classic-db) into quests.
// Accept text is Details, progress text is RequestItemsText, complete text is OfferRewardText.
// The quest giver is the first NPC (or object, e.g. a wanted poster) that starts the quest,
// and its position is that NPC's or object's first spawn in the world. The ender is the first NPC (or object)
// you hand the quest in to; it speaks the progress and complete text.
// cMaNGOS has no NPC races, so they come from the game's own tables (NpcRaceReader), by the NPC's model.
public static class CMangosQuestReader
{
    private const string Unknown = "Unknown";

    // RequiredRaces is a bit mask of playable races.
    private const int AllianceRaces = 1 | 4 | 8 | 64;    // Human, Dwarf, Night Elf, Gnome
    private const int HordeRaces = 2 | 16 | 32 | 128;    // Orc, Undead, Tauren, Troll

    // Titles of quests that were never used in the game, or are test and placeholder quests.
    private static readonly string[] UnusedTitleMarkers =
        ["UNUSED", "DEPRECATED", "<NYI>", "<TXT>", "<TEST>", "[PH]", "<CHANGE TO GOSSIP>", "ZZOLD", "<NOT IN USE>"];

    // creature_template.CreatureType, used as the race of NPCs whose model isn't a playable race.
    private static readonly Dictionary<int, string> CreatureTypes = new()
    {
        [1] = "Beast", [2] = "Dragonkin", [3] = "Demon", [4] = "Elemental", [5] = "Giant",
        [6] = "Undead", [7] = "Humanoid", [8] = "Critter", [9] = "Mechanical",
    };

    private sealed record Npc(string Name, string Race, string Gender, WorldLocation? Location)
    {
        public QuestNpc ToQuestNpc() => new(Name, Race, Gender);
    }

    // raceByModel: the race each NPC model shows, from NpcRaceReader.
    public static QuestReadResult Read(string dumpPath, IReadOnlyDictionary<int, string> raceByModel)
    {
        var tables = MySqlDumpReader.ReadTables(dumpPath, new Dictionary<string, string[]>
        {
            ["quest_template"] = ["entry", "Title", "Details", "RequestItemsText", "OfferRewardText",
                                  "ZoneOrSort", "QuestLevel", "MinLevel", "RequiredRaces"],
            ["creature_questrelation"] = ["id", "quest"],
            ["gameobject_questrelation"] = ["id", "quest"],
            ["creature_involvedrelation"] = ["id", "quest"],
            ["gameobject_involvedrelation"] = ["id", "quest"],
            ["creature_template"] = ["Entry", "Name", "ModelId1", "CreatureType"],
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

        var npcs = new Dictionary<int, Npc>();
        foreach (var row in tables["creature_template"])
        {
            int entry = ToInt(row[0]);
            int model = ToInt(row[2]);
            npcs[entry] = new Npc(
                row[1] ?? Unknown,
                raceByModel.GetValueOrDefault(model) ?? CreatureTypes.GetValueOrDefault(ToInt(row[3]), Unknown),
                genderByModel.GetValueOrDefault(model, Unknown),
                npcSpawns.TryGetValue(entry, out var spawn) ? spawn : null);
        }

        var objects = new Dictionary<int, Npc>();
        foreach (var row in tables["gameobject_template"])
        {
            int entry = ToInt(row[0]);
            objects[entry] = new Npc(
                row[1] ?? Unknown,
                Unknown,
                Unknown,
                objectSpawns.TryGetValue(entry, out var spawn) ? spawn : null);
        }

        var giverByQuest = FirstNpcByQuest(tables["creature_questrelation"], tables["gameobject_questrelation"], npcs, objects);
        var enderByQuest = FirstNpcByQuest(tables["creature_involvedrelation"], tables["gameobject_involvedrelation"], npcs, objects);

        var quests = new List<Quest>();
        var skipped = new Dictionary<string, int>();
        var noGiver = new Npc(Unknown, Unknown, Unknown, null);

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

            Npc giver = giverByQuest.GetValueOrDefault(id, noGiver);
            int zoneOrSort = ToInt(row[5]);
            int questLevel = ToInt(row[6]);
            int minLevel = ToInt(row[7]);

            quests.Add(new Quest(
                id,
                title,
                giver.Name,
                giver.Race,
                giver.Gender,
                acceptText,
                progressText,
                completeText,
                QuestSource.CMangos,
                level: questLevel > 0 ? questLevel : null,   // -1 means the quest scales with your level
                minLevel: minLevel > 0 ? minLevel : null,
                side: SideFromRaces(ToInt(row[8])),
                categoryId: zoneOrSort != 0 ? zoneOrSort : null,
                giverLocation: giver.Location,
                ender: enderByQuest.GetValueOrDefault(id)?.ToQuestNpc()));
        }

        return new QuestReadResult(quests, skipped);
    }

    // Several NPCs (or objects) can start or end the same quest. Use the NPC with the lowest ID, so the result
    // is stable, and an object only when no NPC does.
    private static Dictionary<int, Npc> FirstNpcByQuest(
        List<string?[]> npcRelations, List<string?[]> objectRelations, Dictionary<int, Npc> npcs, Dictionary<int, Npc> objects)
    {
        var result = new Dictionary<int, Npc>();
        foreach (var row in npcRelations.OrderBy(row => ToInt(row[0])))
        {
            int quest = ToInt(row[1]);
            if (!result.ContainsKey(quest) && npcs.TryGetValue(ToInt(row[0]), out var npc))
                result[quest] = npc;
        }
        foreach (var row in objectRelations.OrderBy(row => ToInt(row[0])))
        {
            int quest = ToInt(row[1]);
            if (!result.ContainsKey(quest) && objects.TryGetValue(ToInt(row[0]), out var gameObject))
                result[quest] = gameObject;
        }
        return result;
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
