using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using LiftLog.Api.Models;

namespace LiftLog.Api.Service.Coach;

public sealed class SessionCoachPlanner(
    ICoachChatClient chatClient,
    IReadOnlyList<CoachProvider> providers,
    ILogger<SessionCoachPlanner> logger
) : IAiChatPlannerV2
{
    private const string SystemPrompt = """
        You are the Mission Winning coach. Plan only the next gym session from the athlete's history.
        Reply with one JSON object and nothing else, in this shape:
        {"name":"short name","why":"plain language explanation of the choices","exercises":[{"name":"Exercise","sets":3,"reps":8,"weightKg":60}]}
        Keep the exercise names from the history. Prefer the same movements as the most recent session unless the athlete asked for a change.
        If the last session was 7 or more days ago, choose lighter loads. The server still reduces every load by 10 percent in that case.
        Do not discuss nutrition. Do not use markdown.
        """;

    private CancellationTokenSource? chatStopToken;

    public Task Introduce(
        string locale,
        string preferredWeightUnit,
        Func<AiChatResponseV2, Task> callback
    ) =>
        SendAsync(
            $"Introduce yourself in the locale {locale}. Preferred weight unit: {preferredWeightUnit}. Ask what they want from the next session.",
            callback
        );

    public Task SendMessageAsync(string userMessage, Func<AiChatResponseV2, Task> callback) =>
        SendAsync(userMessage, callback);

    public Task ClearConversationAsync()
    {
        chatStopToken?.Cancel();
        chatStopToken = null;
        return Task.CompletedTask;
    }

    public void StopInProgress() => chatStopToken?.Cancel();

    private async Task SendAsync(string userMessage, Func<AiChatResponseV2, Task> callback)
    {
        chatStopToken?.Cancel();
        chatStopToken = new CancellationTokenSource();
        var cancellation = chatStopToken.Token;
        var (history, userText) = CoachHistoryText.Split(userMessage);
        try
        {
            var prompt = history is null
                ? userMessage
                : $"History JSON:\n{JsonSerializer.Serialize(history, CoachJson.Options)}\n\nAthlete: {userText}";
            var completion = await chatClient.CompleteAsync(
                providers,
                SystemPrompt,
                prompt,
                cancellation
            );
            var proposal = CoachProposalParser.Parse(completion);
            var result = MissedDayLoad.Apply(history, proposal);
            if (result.Exercises.Count == 0)
            {
                await callback(new AiChatMessageResponseV2(result.Why));
                return;
            }
            await callback(
                new AiChatPlanResponseV2(
                    result.Name,
                    result.Why,
                    BuildBlueprint(result),
                    new AiPlanToolProvider().CurrentAiPlanVersion
                )
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI coach completion failed");
            var fallback = MissedDayLoad.Apply(history, new CoachProposal("Next session", "", []));
            if (fallback.Exercises.Count > 0)
            {
                await callback(
                    new AiChatPlanResponseV2(
                        fallback.Name,
                        fallback.Why,
                        BuildBlueprint(fallback),
                        new AiPlanToolProvider().CurrentAiPlanVersion
                    )
                );
                return;
            }
            await callback(
                new AiChatMessageResponseV2(
                    "The AI coach could not reach a model. Set LITELLM_BASE_URL, GROQ_API_KEY, or OPENROUTER_API_KEY on the server."
                )
            );
        }
    }

    internal static JsonObject BuildBlueprint(CoachResult result)
    {
        var exercises = new JsonArray();
        foreach (var exercise in result.Exercises)
        {
            var sets = new JsonArray();
            var count = Math.Max(1, exercise.Sets);
            for (var i = 0; i < count; i++)
            {
                var reps = Math.Max(1, exercise.Reps);
                sets.Add(
                    new JsonObject
                    {
                        ["reps"] = new JsonObject { ["min"] = reps, ["max"] = reps },
                    }
                );
            }
            exercises.Add(
                new JsonObject
                {
                    ["type"] = "WeightedExerciseBlueprint",
                    ["name"] = exercise.Name,
                    ["notes"] =
                        $"Target load: {exercise.WeightKg.ToString("0.##", CultureInfo.InvariantCulture)} kg",
                    ["plannedSets"] = sets,
                    ["resistance"] = "external",
                }
            );
        }
        return new JsonObject
        {
            ["name"] = result.Name,
            ["sessions"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = result.Name,
                    ["notes"] = "",
                    ["exercises"] = exercises,
                },
            },
        };
    }
}
