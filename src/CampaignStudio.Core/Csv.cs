using System.Text;

namespace CampaignStudio.Core;

public static class Csv
{
    public static List<string[]> Parse(string source)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false, endedQuote = false, started = false;
        source = source.TrimStart('\uFEFF');
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; endedQuote = true; }
                }
                else cell.Append(c);
                continue;
            }
            if (c == '"' && !started) { quoted = true; started = true; continue; }
            if (c is ',' or '\r' or '\n')
            {
                row.Add(cell.ToString()); cell.Clear(); endedQuote = started = false;
                if (c != ',')
                {
                    if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                    if (row.Any(x => x.Length > 0)) rows.Add(row.ToArray());
                    row.Clear();
                }
                continue;
            }
            if (endedQuote || c == '"') throw new FormatException("Invalid quoted CSV field.");
            cell.Append(c); started = true;
        }
        if (quoted) throw new FormatException("The CSV contains an unfinished quoted field.");
        if (started || endedQuote || cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
}
