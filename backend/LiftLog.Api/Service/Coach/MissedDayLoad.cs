namespace LiftLog.Api.Service.Coach;

/// <summary>
/// Missed-day rule for the coach. Seven or more days since the last session
/// cuts every proposed load by 10 percent. The model may suggest that itself;
/// this still runs afterwards so the reduction does not depend on the model.
/// </summary>
public static class MissedDayLoad
{
    public const int DeloadAfterDays = 7;
    public const decimal DeloadFactor = 0.9m;

    public static bool ShouldDeload(int daysSinceLastSession) =>
        daysSinceLastSession >= DeloadAfterDays;

    public static decimal Adjust(decimal weightKg, bool deload) =>
        deload
            ? decimal.Round(weightKg * DeloadFactor, 2, MidpointRounding.AwayFromZero)
            : weightKg;

    public static int? DaysSince(CoachHistory? history)
    {
        if (history?.LastSessionDate is null)
        {
            return null;
        }
        if (!DateOnly.TryParse(history.LastSessionDate, out var last))
        {
            return null;
        }
        var today = DateOnly.TryParse(history.Today, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);
        return today.DayNumber - last.DayNumber;
    }

    public static CoachResult Apply(CoachHistory? history, CoachProposal proposal)
    {
        var days = DaysSince(history);
        var deload = days is int gap && ShouldDeload(gap);
        var source =
            proposal.Exercises.Count > 0
                ? proposal.Exercises
                : history?.Sessions.FirstOrDefault()?.Exercises.Select(ToProposed).ToList() ?? [];
        var exercises = source
            .Select(exercise => exercise with { WeightKg = Adjust(exercise.WeightKg, deload) })
            .ToList();
        return new CoachResult(
            string.IsNullOrWhiteSpace(proposal.Name) ? "Next session" : proposal.Name.Trim(),
            Explain(proposal.Why, days, deload),
            exercises,
            deload,
            days
        );
    }

    private static ProposedExercise ToProposed(CoachHistoryExercise exercise) =>
        new(exercise.Name, exercise.Sets, exercise.Reps, exercise.WeightKg);

    public static string Explain(string? modelWhy, int? days, bool deload)
    {
        var why = (modelWhy ?? "").Trim();
        if (days is null)
        {
            return why.Length > 0
                ? why
                : "There is no recent session to compare, so this is a fresh plan.";
        }
        var rule = deload
            ? $"Load reduced by 10% because the last session was {days} days ago."
            : $"Last session was {days} days ago, so the load is unchanged.";
        return why.Length > 0 ? $"{why} {rule}" : rule;
    }
}
