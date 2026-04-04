using System.Text;

namespace Pw.Hub.Tracker.Infrastructure.Helpers;

public static class HomoglyphHelper
{
    private static readonly Dictionary<char, char> Homoglyphs = new()
    {
        { 'а', 'a' }, { 'А', 'A' },
        { 'е', 'e' }, { 'Е', 'E' },
        { 'о', 'o' }, { 'О', 'O' },
        { 'с', 'c' }, { 'С', 'C' },
        { 'р', 'p' }, { 'Р', 'P' },
        { 'х', 'x' }, { 'Х', 'X' },
        { 'у', 'y' }, { 'У', 'Y' },
        { 'к', 'k' }, { 'К', 'K' },
        { 'Н', 'H' },
        { 'В', 'B' },
        { 'М', 'M' },
        { 'Т', 'T' },
        // Reverse mapping to handle both directions if needed
        { 'a', 'а' }, { 'A', 'А' },
        { 'e', 'е' }, { 'E', 'Е' },
        { 'o', 'о' }, { 'O', 'О' },
        { 'c', 'с' }, { 'C', 'С' },
        { 'p', 'р' }, { 'P', 'Р' },
        { 'x', 'х' }, { 'X', 'Х' },
        { 'y', 'у' }, { 'Y', 'У' },
        { 'k', 'к' }, { 'K', 'К' },
        { 'H', 'Н' },
        { 'B', 'В' },
        { 'M', 'М' },
        { 'T', 'Т' }
    };

    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (Homoglyphs.TryGetValue(c, out var normalized))
            {
                sb.Append(normalized);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
