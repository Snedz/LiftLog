using System.Text.Json;

namespace LiftLog.Api.Service.Coach;

public static class CoachProposalParser
{
    public static CoachProposal Parse(string modelText)
    {
        var json = ExtractObject(modelText);
        if (json is null)
        {
            return new CoachProposal("Next session", modelText.Trim(), []);
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var name = ReadString(root, "name") ?? "Next session";
            var why = ReadString(root, "why") ?? "";
            var exercises = new List<ProposedExercise>();
            if (
                root.TryGetProperty("exercises", out var list)
                && list.ValueKind == JsonValueKind.Array
            )
            {
                foreach (var item in list.EnumerateArray())
                {
                    var exerciseName = ReadString(item, "name");
                    if (string.IsNullOrWhiteSpace(exerciseName))
                    {
                        continue;
                    }
                    exercises.Add(
                        new ProposedExercise(
                            exerciseName,
                            Math.Max(1, ReadInt(item, "sets") ?? 1),
                            Math.Max(1, ReadInt(item, "reps") ?? 1),
                            ReadDecimal(item, "weightKg") ?? 0
                        )
                    );
                }
            }
            return new CoachProposal(name, why, exercises);
        }
        catch (JsonException)
        {
            return new CoachProposal("Next session", modelText.Trim(), []);
        }
    }

    private static string? ExtractObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }
        return text[start..(end + 1)];
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null,
        };
    }

    private static decimal? ReadDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var number) => number,
            _ => null,
        };
    }
}
