using System.Collections.Concurrent;
using AutomaticInterface;
using LiftLog.Api.Service.Coach;

namespace LiftLog.Api.Service;

[GenerateAutomaticInterface]
public class AiChatDirectoryV2(
    ICoachChatClient chatClient,
    IConfiguration configuration,
    ILoggerFactory loggerFactory
) : IAiChatDirectoryV2
{
    private readonly ConcurrentDictionary<string, IAiChatPlannerV2> chatSessions = new();

    public IAiChatPlannerV2 GetChat(string connectionId)
    {
        return chatSessions.GetOrAdd(
            connectionId,
            _ => new SessionCoachPlanner(
                chatClient,
                CoachProviderCatalog.FromConfiguration(configuration),
                loggerFactory.CreateLogger<SessionCoachPlanner>()
            )
        );
    }

    public async Task CloseChatAsync(string connectionId)
    {
        if (chatSessions.Remove(connectionId, out var chat))
        {
            await chat.ClearConversationAsync();
        }
    }
}
