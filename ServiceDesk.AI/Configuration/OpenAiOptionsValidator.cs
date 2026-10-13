using Microsoft.Extensions.Options;

namespace ServiceDesk.AI.Configuration;

public sealed class OpenAiOptionsValidator : IValidateOptions<OpenAiOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAiOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("OpenAI:ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("OpenAI:Model is required.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps
            || !baseUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            || !string.IsNullOrEmpty(baseUri.UserInfo)
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            failures.Add(
                "OpenAI:BaseUrl must be an absolute HTTPS directory URL " +
                "without credentials, query parameters, or a fragment.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}