using System.Net;
using System.Text;
using LiftLog.Api.Service.Coach;
using Microsoft.Extensions.Configuration;

namespace LiftLog.Tests.Api.Unit;

public class SessionCoachTests
{
    [Test]
    [Category("Unit")]
    public async Task MissedWeek_ReducesEveryLoadByTenPercent()
    {
        var history = History(today: "2026-10-14", last: "2026-10-01", weight: 100);
        var proposal = new CoachProposal(
            "Next session",
            "Kept the squat.",
            [new ProposedExercise("Squat", 3, 5, 100)]
        );

        var result = MissedDayLoad.Apply(history, proposal);

        await Assert.That(result.Deloaded).IsTrue();
        await Assert.That(result.DaysSinceLastSession).IsEqualTo(13);
        await Assert.That(result.Exercises[0].WeightKg).IsEqualTo(90m);
        await Assert.That(result.Why).Contains("Load reduced by 10%");
        await Assert.That(result.Why).Contains("Kept the squat.");
    }

    [Test]
    [Category("Unit")]
    public async Task RecentSession_LeavesLoadAlone()
    {
        var history = History(today: "2026-10-14", last: "2026-10-08", weight: 80);
        var proposal = new CoachProposal(
            "Next",
            "You trained six days ago.",
            [new ProposedExercise("Bench Press", 4, 8, 80)]
        );

        var result = MissedDayLoad.Apply(history, proposal);

        await Assert.That(result.Deloaded).IsFalse();
        await Assert.That(result.Exercises[0].WeightKg).IsEqualTo(80m);
        await Assert.That(result.Why).Contains("load is unchanged");
    }

    [Test]
    [Category("Unit")]
    public async Task SeventhDay_IsTheFirstDeload()
    {
        await Assert.That(MissedDayLoad.ShouldDeload(6)).IsFalse();
        await Assert.That(MissedDayLoad.ShouldDeload(7)).IsTrue();
        await Assert.That(MissedDayLoad.Adjust(100, deload: true)).IsEqualTo(90m);
    }

    [Test]
    [Category("Unit")]
    public async Task EmptyModelPlan_CopiesTheLastSessionThenDeloads()
    {
        var history = History(today: "2026-10-14", last: "2026-09-01", weight: 50);
        var result = MissedDayLoad.Apply(history, new CoachProposal("Next session", "", []));

        await Assert.That(result.Exercises.Count).IsEqualTo(1);
        await Assert.That(result.Exercises[0].Name).IsEqualTo("Squat");
        await Assert.That(result.Exercises[0].WeightKg).IsEqualTo(45m);
    }

    [Test]
    [Category("Unit")]
    public async Task HistoryBlock_IsSplitOutOfTheUserText()
    {
        var (history, user) = CoachHistoryText.Split(
            """
            <<mw-coach-history>>
            {"today":"2026-10-14","lastSessionDate":"2026-10-01","sessions":[]}
            <<end-mw-coach-history>>
            Plan the next one
            """
        );

        await Assert.That(history).IsNotNull();
        await Assert.That(history!.LastSessionDate).IsEqualTo("2026-10-01");
        await Assert.That(user).IsEqualTo("Plan the next one");
    }

    [Test]
    [Category("Unit")]
    public async Task ProviderOrder_IsLiteLlmThenGroqThenOpenRouterFree()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["LITELLM_BASE_URL"] = "http://litellm.internal/v1",
                    ["GROQ_API_KEY"] = "test-groq-key",
                    ["OPENROUTER_API_KEY"] = "test-openrouter-key",
                }
            )
            .Build();

        var providers = CoachProviderCatalog.FromConfiguration(configuration);

        await Assert.That(providers[0].Name).IsEqualTo("litellm");
        await Assert.That(providers[1].Name).IsEqualTo("groq");
        await Assert.That(providers[2].Name).IsEqualTo("openrouter");
        await Assert.That(providers[1].Model).IsEqualTo(CoachProviderCatalog.GroqModel);
        await Assert.That(providers[2].Model.EndsWith(":free", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task ChatClient_FallsThroughToTheNextProvider()
    {
        var handler = new ScriptedHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        handler.Responses.Enqueue(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"{\"name\":\"Next\",\"why\":\"Because you asked.\",\"exercises\":[]}"}}]}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            }
        );
        var client = new OpenAiCompatibleChatClient(handler);
        var providers = new CoachProvider[]
        {
            new("litellm", "http://litellm.test/v1", "test-litellm-key", "gpt-oss-120b"),
            new(
                "groq",
                "https://api.groq.com/openai/v1",
                "test-groq-key",
                CoachProviderCatalog.GroqModel
            ),
        };

        var content = await client.CompleteAsync(
            providers,
            "system",
            "user",
            CancellationToken.None
        );

        await Assert.That(content).Contains("Because you asked.");
        await Assert.That(handler.Requests.Count).IsEqualTo(2);
        await Assert.That(handler.Requests[0].RequestUri!.Host).IsEqualTo("litellm.test");
        await Assert.That(handler.Requests[1].RequestUri!.Host).IsEqualTo("api.groq.com");
        await Assert
            .That(handler.Requests[1].Headers.Authorization!.Parameter)
            .IsEqualTo("test-groq-key");
    }

    private static CoachHistory History(string today, string last, decimal weight) =>
        new(
            today,
            last,
            [
                new CoachHistorySession(
                    last,
                    "Legs",
                    [new CoachHistoryExercise("Squat", 3, 5, weight)]
                ),
            ]
        );

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public Queue<HttpResponseMessage> Responses { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(request);
            return Task.FromResult(Responses.Dequeue());
        }
    }
}
