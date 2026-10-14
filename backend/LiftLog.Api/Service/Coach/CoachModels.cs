using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiftLog.Api.Service.Coach;

public sealed record CoachHistoryExercise(string Name, int Sets, int Reps, decimal WeightKg);

public sealed record CoachHistorySession(
    string Date,
    string Name,
    List<CoachHistoryExercise> Exercises
);

public sealed record CoachHistory(
    string? Today,
    string? LastSessionDate,
    List<CoachHistorySession> Sessions
);

public sealed record ProposedExercise(string Name, int Sets, int Reps, decimal WeightKg);

public sealed record CoachProposal(string Name, string Why, List<ProposedExercise> Exercises);

public sealed record CoachResult(
    string Name,
    string Why,
    List<ProposedExercise> Exercises,
    bool Deloaded,
    int? DaysSinceLastSession
);

public static class CoachJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
