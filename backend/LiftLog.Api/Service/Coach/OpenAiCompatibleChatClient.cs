using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LiftLog.Api.Service.Coach;

public interface ICoachChatClient
{
    Task<string> CompleteAsync(
        IReadOnlyList<CoachProvider> providers,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken
    );
}

public sealed class CoachUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Posts chat completions to any OpenAI-compatible base URL. Tries each
/// configured provider in order and uses the first one that returns a message.
/// </summary>
public sealed class OpenAiCompatibleChatClient(HttpMessageHandler handler) : ICoachChatClient
{
    public async Task<string> CompleteAsync(
        IReadOnlyList<CoachProvider> providers,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken
    )
    {
        if (providers.Count == 0)
        {
            throw new CoachUnavailableException(
                "No AI coach provider is configured. Set LITELLM_BASE_URL, GROQ_API_KEY, or OPENROUTER_API_KEY on the server."
            );
        }

        Exception? last = null;
        foreach (var provider in providers)
        {
            try
            {
                return await CompleteOneAsync(
                    provider,
                    systemPrompt,
                    userPrompt,
                    cancellationToken
                );
            }
            catch (Exception ex)
                when (ex
                        is HttpRequestException
                            or TaskCanceledException
                            or InvalidOperationException
                )
            {
                last = ex;
            }
        }

        throw new CoachUnavailableException("Every configured AI coach provider failed.", last);
    }

    private async Task<string> CompleteOneAsync(
        CoachProvider provider,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken
    )
    {
        using var client = new HttpClient(handler, disposeHandler: false);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{provider.BaseUrl}/chat/completions"
        );
        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                provider.ApiKey
            );
        }
        request.Content = new StringContent(
            JsonSerializer.Serialize(
                new
                {
                    model = provider.Model,
                    temperature = 0.2,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt },
                    },
                }
            ),
            Encoding.UTF8,
            "application/json"
        );

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{provider.Name} returned {(int)response.StatusCode}.");
        }
        return ReadContent(body);
    }

    internal static string ReadContent(string body)
    {
        using var document = JsonDocument.Parse(body);
        var content = document
            .RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("The coach provider returned an empty message.");
        }
        return content;
    }
}
