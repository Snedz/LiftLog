namespace LiftLog.Api.Service.Coach;

public sealed record CoachProvider(string Name, string BaseUrl, string? ApiKey, string Model);

/// <summary>
/// Provider order is LiteLLM, then Groq <c>gpt-oss-120b</c>, then an OpenRouter
/// model whose id ends in <c>:free</c>. A provider is included only when its
/// key or, for LiteLLM, its base URL is set in configuration. Those values
/// come from the process environment. Nothing here has a default key.
/// </summary>
public static class CoachProviderCatalog
{
    public const string GroqModel = "openai/gpt-oss-120b";
    public const string OpenRouterFreeModel = "meta-llama/llama-3.3-70b-instruct:free";

    public static IReadOnlyList<CoachProvider> FromConfiguration(IConfiguration configuration)
    {
        var providers = new List<CoachProvider>();

        var liteLlmBase = First(configuration, "AiPlanner:LiteLlm:BaseUrl", "LITELLM_BASE_URL");
        if (!string.IsNullOrWhiteSpace(liteLlmBase))
        {
            providers.Add(
                new CoachProvider(
                    "litellm",
                    liteLlmBase.TrimEnd('/'),
                    BlankToNull(
                        First(configuration, "AiPlanner:LiteLlm:ApiKey", "LITELLM_API_KEY")
                    ),
                    First(configuration, "AiPlanner:LiteLlm:Model", "LITELLM_MODEL")
                        ?? "gpt-oss-120b"
                )
            );
        }

        var groqKey = First(configuration, "AiPlanner:Groq:ApiKey", "GROQ_API_KEY");
        if (!string.IsNullOrWhiteSpace(groqKey))
        {
            providers.Add(
                new CoachProvider(
                    "groq",
                    (
                        First(configuration, "AiPlanner:Groq:BaseUrl", "GROQ_BASE_URL")
                        ?? "https://api.groq.com/openai/v1"
                    ).TrimEnd('/'),
                    groqKey,
                    First(configuration, "AiPlanner:Groq:Model", "GROQ_MODEL") ?? GroqModel
                )
            );
        }

        var openRouterKey = First(
            configuration,
            "AiPlanner:OpenRouter:ApiKey",
            "OPENROUTER_API_KEY"
        );
        if (!string.IsNullOrWhiteSpace(openRouterKey))
        {
            providers.Add(
                new CoachProvider(
                    "openrouter",
                    (
                        First(configuration, "AiPlanner:OpenRouter:BaseUrl", "OPENROUTER_BASE_URL")
                        ?? "https://openrouter.ai/api/v1"
                    ).TrimEnd('/'),
                    openRouterKey,
                    First(configuration, "AiPlanner:OpenRouter:Model", "OPENROUTER_MODEL")
                        ?? OpenRouterFreeModel
                )
            );
        }

        return providers;
    }

    private static string? First(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }
        return null;
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
