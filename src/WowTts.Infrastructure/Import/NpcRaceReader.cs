using System.Globalization;

namespace WowTts.Infrastructure.Import;

// Reads which race each NPC model shows, from game client tables that wago.tools exports as CSV:
//   CreatureDisplayInfo: an NPC model, and for NPCs that look like a playable race, its ExtendedDisplayInfoID
//   CreatureDisplayInfoExtra: that look's race (DisplayRaceID)
//   ChrRaces: the race names
// cMaNGOS stores only an NPC's model, not its race, because the server never needs it. This gives 92% of
// quest givers a race; the rest have a model of their own (ogres, dragons, elementals…).
public static class NpcRaceReader
{
    public static readonly string[] Tables = ["CreatureDisplayInfo", "CreatureDisplayInfoExtra", "ChrRaces"];

    // The race by NPC model ID (cMaNGOS's ModelId1), e.g. "Orc" or "NightElf".
    public static IReadOnlyDictionary<int, string> Read(string folder)
    {
        var raceNames = new Dictionary<int, string>();
        foreach (var row in CsvFile.Read(Path.Combine(folder, "ChrRaces.csv")))
            raceNames[ToInt(row["ID"])] = row["Name_lang"].Replace(" ", "");   // "Night Elf" -> "NightElf"

        var raceByLook = new Dictionary<int, string>();
        foreach (var row in CsvFile.Read(Path.Combine(folder, "CreatureDisplayInfoExtra.csv")))
        {
            if (raceNames.TryGetValue(ToInt(row["DisplayRaceID"]), out string? race))
                raceByLook[ToInt(row["ID"])] = race;
        }

        var raceByModel = new Dictionary<int, string>();
        foreach (var row in CsvFile.Read(Path.Combine(folder, "CreatureDisplayInfo.csv")))
        {
            if (raceByLook.TryGetValue(ToInt(row["ExtendedDisplayInfoID"]), out string? race))
                raceByModel[ToInt(row["ID"])] = race;
        }

        return raceByModel;
    }

    private static int ToInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : 0;
}
