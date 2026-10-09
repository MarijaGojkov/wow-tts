using System.IO.Compression;
using System.Text;

namespace WowTts.Infrastructure.Import;

// Reads rows of chosen tables straight from a MySQL dump (.sql or .sql.gz), without installing MySQL.
// It understands the mysqldump layout: a CREATE TABLE block listing the columns, then
// INSERT INTO `table` VALUES (...),(...); lines with the rows.
public static class MySqlDumpReader
{
    // wanted maps a table name to the columns to keep. Each returned row holds those columns, in that order.
    // NULL values come back as null.
    public static Dictionary<string, List<string?[]>> ReadTables(
        string path, IReadOnlyDictionary<string, string[]> wanted)
    {
        var rows = wanted.Keys.ToDictionary(table => table, _ => new List<string?[]>());
        var columnIndexes = new Dictionary<string, int[]>();

        string? tableBeingCreated = null;
        var createdColumns = new List<string>();

        using StreamReader reader = OpenText(path);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (tableBeingCreated is not null)
            {
                // Inside CREATE TABLE: column lines start with a backtick, the block ends with ")".
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith('`'))
                {
                    createdColumns.Add(trimmed[1..trimmed.IndexOf('`', 1)]);
                }
                else if (trimmed.StartsWith(')'))
                {
                    columnIndexes[tableBeingCreated] =
                        FindColumns(tableBeingCreated, createdColumns, wanted[tableBeingCreated]);
                    tableBeingCreated = null;
                }
                continue;
            }

            if (line.StartsWith("CREATE TABLE `"))
            {
                string table = TableName(line, "CREATE TABLE `");
                if (wanted.ContainsKey(table))
                {
                    tableBeingCreated = table;
                    createdColumns = [];
                }
                continue;
            }

            if (line.StartsWith("INSERT INTO `"))
            {
                string table = TableName(line, "INSERT INTO `");
                if (!wanted.ContainsKey(table))
                    continue;

                if (!columnIndexes.TryGetValue(table, out int[]? indexes))
                    throw new InvalidDataException($"The dump inserts into `{table}` before creating it.");

                foreach (string?[] values in ParseValues(line, table))
                    rows[table].Add(indexes.Select(index => values[index]).ToArray());
            }
        }

        var missing = wanted.Keys.Where(table => !columnIndexes.ContainsKey(table)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"The dump has no table named {string.Join(", ", missing)}.");

        return rows;
    }

    private static StreamReader OpenText(string path)
    {
        Stream stream = File.OpenRead(path);
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            stream = new GZipStream(stream, CompressionMode.Decompress);

        return new StreamReader(stream, Encoding.UTF8);
    }

    private static string TableName(string line, string prefix) =>
        line[prefix.Length..line.IndexOf('`', prefix.Length)];

    private static int[] FindColumns(string table, List<string> columns, string[] wanted) =>
        wanted.Select(name =>
        {
            int index = columns.FindIndex(column => column.Equals(name, StringComparison.OrdinalIgnoreCase));
            return index >= 0
                ? index
                : throw new InvalidDataException($"Table `{table}` has no column `{name}`.");
        }).ToArray();

    // Splits "INSERT INTO `t` VALUES (1,'a',NULL),(2,'b\'c',3);" into rows of values.
    private static IEnumerable<string?[]> ParseValues(string line, string table)
    {
        int start = line.IndexOf(" VALUES ", StringComparison.Ordinal);
        if (start < 0 || line.IndexOf('(', 0, start) >= 0)
            throw new InvalidDataException($"Unsupported INSERT format for `{table}`. Expected INSERT INTO `{table}` VALUES (...).");

        var values = new List<string?>();
        var value = new StringBuilder();
        bool inRow = false, inString = false, wasQuoted = false;

        for (int i = start + " VALUES ".Length; i < line.Length; i++)
        {
            char c = line[i];

            if (inString)
            {
                if (c == '\\' && i + 1 < line.Length)
                    value.Append(Unescape(line[++i]));
                else if (c == '\'' && i + 1 < line.Length && line[i + 1] == '\'')
                    value.Append(line[++i]);
                else if (c == '\'')
                    inString = false;
                else
                    value.Append(c);
                continue;
            }

            switch (c)
            {
                case '(' when !inRow:
                    inRow = true;
                    values.Clear();
                    break;
                case '\'' when inRow:
                    inString = true;
                    wasQuoted = true;
                    break;
                case ',' when inRow:
                    values.Add(TakeValue());
                    break;
                case ')' when inRow:
                    values.Add(TakeValue());
                    yield return values.ToArray();
                    inRow = false;
                    break;
                default:
                    if (inRow)
                        value.Append(c);
                    break;
            }
        }

        string? TakeValue()
        {
            string raw = value.ToString();
            bool quoted = wasQuoted;
            value.Clear();
            wasQuoted = false;

            if (quoted)
                return raw;

            raw = raw.Trim();
            return raw.Equals("NULL", StringComparison.OrdinalIgnoreCase) ? null : raw;
        }
    }

    // MySQL string escapes: \n, \r, \t, \0, \b, \Z, and \' \" \\ for the character itself.
    private static char Unescape(char c) => c switch
    {
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        '0' => '\0',
        'b' => '\b',
        'Z' => (char)26,
        _ => c
    };
}
