using System.Text;

namespace Pw.Hub.Tracker.Infrastructure.Helpers;

public static class ArenaDecoder
{
    public static string DecodeTeamName(string base64Name)
    {
        var raw = Convert.FromBase64String(base64Name);
        return Encoding.Unicode.GetString(raw);
    }

    public static long DecodeMatchId(string base64Data)
    {
        var raw = Convert.FromBase64String(base64Data);
        return BitConverter.ToInt64(raw, 0);
    }
}
