using System.Text.Json;

namespace LiftLog.Api.Service.Coach;

public static class CoachHistoryText
{
    public const string Start = "<<mw-coach-history>>";
    public const string End = "<<end-mw-coach-history>>";

    public static (CoachHistory? History, string UserText) Split(string message)
    {
        var start = message.IndexOf(Start, StringComparison.Ordinal);
        var end = message.IndexOf(End, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            return (null, message.Trim());
        }
        var json = message[(start + Start.Length)..end].Trim();
        var user = message[(end + End.Length)..].Trim();
        try
        {
            var history = JsonSerializer.Deserialize<CoachHistory>(json, CoachJson.Options);
            return (history, user);
        }
        catch (JsonException)
        {
            return (null, message.Trim());
        }
    }
}
