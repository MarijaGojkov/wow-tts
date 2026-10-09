namespace WowTts.Infrastructure.Import;

// Reads a CSV file with a header row (as exported by wago.tools) into rows keyed by column name.
// Handles quoted values with commas and doubled quotes, e.g. "Un'Goro Crater" or "The ""Bad"" Lands".
public static class CsvFile
{
    public static List<Dictionary<string, string>> Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Game data file not found: {path}. Run the importer's gamedata command first.");

        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            return [];

        List<string> header = ParseLine(lines[0]);
        var rows = new List<Dictionary<string, string>>();

        foreach (string line in lines.Skip(1).Where(line => line.Length > 0))
        {
            List<string> values = ParseLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Count; i++)
                row[header[i]] = i < values.Count ? values[i] : "";
            rows.Add(row);
        }

        return rows;
    }

    private static List<string> ParseLine(string line)
    {
        var values = new List<string>();
        var value = new System.Text.StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    value.Append(line[++i]);
                else if (c == '"')
                    quoted = false;
                else
                    value.Append(c);
            }
            else if (c == '"')
                quoted = true;
            else if (c == ',')
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
                value.Append(c);
        }

        values.Add(value.ToString());
        return values;
    }
}
