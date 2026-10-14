using System.Collections.Concurrent;
using LiftLog.Api.Authentication;
using LiftLog.Api.Models;
using LiftLog.Api.Service;
using LiftLog.Api.Service.Coach;
using LiftLog.Tests.Api.Unit.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace LiftLog.Tests.Api.Integration;

/// <summary>
/// Drives the real /ai-chat-v2 hub → directory → coach planner → SignalR path.
/// The language-model client is faked, so it needs no API key.
/// </summary>
[ClassDataSource<WebApplicationFactory<Program>>(Shared = SharedType.PerClass)]
public class AiWorkoutPlannerV2IntegrationTests
{
    private const string TestApiKey = "test-api-key-12345";

    private static readonly int CurrentVersion = new AiPlanToolProvider().CurrentAiPlanVersion;

    private const string ModelJson = """
        {"name":"Next session","why":"Same squat as last time.","exercises":[{"name":"Squat","sets":3,"reps":5,"weightKg":100}]}
        """;

    private readonly WebApplicationFactory<Program> _factory;

    public AiWorkoutPlannerV2IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = TestFactoryHelper.CreateTestFactory(
            factory,
            services =>
            {
                var mockRevenueCatService =
                    Substitute.For<IRevenueCatPurchaseVerificationService>();
                mockRevenueCatService
                    .GetUserIdHasProEntitlementAsync(Arg.Any<string>())
                    .Returns(Task.FromResult(false));
                services.AddSingleton(mockRevenueCatService);

                services.AddSingleton<ICoachChatClient>(new FixedCoachChatClient(ModelJson));
            },
            extraConfiguration: new Dictionary<string, string?>
            {
                [AuthConfiguration.ApiKey.ValuePath] = TestApiKey,
            }
        );
    }

    /// <summary>
    /// Hub messages arrive on the connection's own callbacks, so they can still be in flight when
    /// the InvokeAsync that triggered them returns. Wait for the ones under test rather than
    /// assuming they have landed.
    /// </summary>
    private static async Task WaitForAsync(
        IReadOnlyCollection<AiChatResponseV2> messages,
        Func<IEnumerable<AiChatResponseV2>, bool> predicate,
        int timeoutMs = 5000
    )
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!predicate(messages) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }

    private HubConnection CreateHubConnection()
    {
        var server = _factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(
                $"{server.BaseAddress}ai-chat-v2",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.Headers.Add("X-API-Key", TestApiKey);
                }
            )
            .AddJsonProtocol()
            .Build();
    }

    [Test]
    [Category("Integration")]
    public async Task SendMessage_StreamsTextThenForwardsTheGeneratedPlan()
    {
        await using var hubConnection = CreateHubConnection();
        var receivedMessages = new ConcurrentQueue<AiChatResponseV2>();
        hubConnection.On<AiChatResponseV2>("ReceiveMessage", receivedMessages.Enqueue);

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync(
            "SendMessage",
            """
            <<mw-coach-history>>
            {"today":"2026-10-14","lastSessionDate":"2026-10-01","sessions":[{"date":"2026-10-01","name":"Legs","exercises":[{"name":"Squat","sets":3,"reps":5,"weightKg":100}]}]}
            <<end-mw-coach-history>>
            Plan my next session
            """,
            CurrentVersion
        );
        await WaitForAsync(receivedMessages, m => m.OfType<AiChatPlanResponseV2>().Any());

        var plan = receivedMessages.OfType<AiChatPlanResponseV2>().Single();
        await Assert.That(plan.Name).IsEqualTo("Next session");
        await Assert.That(plan.Description).Contains("Load reduced by 10%");
        await Assert.That(plan.Blueprint).IsNotNull();
        await Assert
            .That(plan.Blueprint!["sessions"]![0]!["exercises"]![0]!["notes"]!.GetValue<string>())
            .IsEqualTo("Target load: 90 kg");
    }

    [Test]
    [Category("Integration")]
    public async Task Introduce_WhenClientOutOfDate_ReturnsUpdateRequired()
    {
        await using var hubConnection = CreateHubConnection();
        var receivedMessages = new ConcurrentQueue<AiChatResponseV2>();
        hubConnection.On<AiChatResponseV2>("ReceiveMessage", receivedMessages.Enqueue);

        await hubConnection.StartAsync();
        // Client reports a version behind the server's current contract version.
        await hubConnection.InvokeAsync("Introduce", "en-US", CurrentVersion - 1, "kg");
        await WaitForAsync(receivedMessages, m => m.OfType<AiChatUpdateRequiredResponseV2>().Any());

        var update = receivedMessages.OfType<AiChatUpdateRequiredResponseV2>().Single();
        await Assert.That(update.RequiredVersion).IsEqualTo(CurrentVersion);
        // No plan or intro message is generated for an out-of-date client.
        await Assert.That(receivedMessages.OfType<AiChatMessageResponseV2>().Any()).IsFalse();
    }

    private sealed class FixedCoachChatClient(string content) : ICoachChatClient
    {
        public Task<string> CompleteAsync(
            IReadOnlyList<CoachProvider> providers,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken
        ) => Task.FromResult(content);
    }
}
