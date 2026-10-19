using Microsoft.Extensions.Options;
using ServiceDesk.AI.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Shell.AiAnalysis;

namespace ServiceDesk.AI.AiAnalysis;

public sealed class OpenAiTicketAnalyzer(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options) : ITicketAiAnalyzer
{
    private static readonly JsonSerializerOptions ResultJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters =
            {
                new JsonStringEnumConverter(allowIntegerValues: false)
            }
        };

    public async Task<TicketAiAnalysisResult> AnalyzeAsync(
        TicketAiAnalysisInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var providerOptions = options.Value;

        var requestBody = new
        {
            model = providerOptions.Model,
            instructions = OpenAiTicketAnalysisPrompt.Instructions,
            input = JsonSerializer.Serialize(new
            {
                title = input.Title,
                description = input.Description
            }),
            store = false,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "ticket_ai_analysis",
                    strict = true,
                    schema = OpenAiTicketAnalysisSchema.Value
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/responses")
        {
            Content = JsonContent.Create(requestBody)
        };

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", providerOptions.ApiKey);

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI returned HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        var responseJson = await response.Content.ReadAsStringAsync(
            cancellationToken);

        return parseResponse(responseJson);
    }

    private static TicketAiAnalysisResult parseResponse(string responseJson)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (getString(root, "status") != "completed")
            {
                throw new OpenAiResponseException(
                    "OpenAI did not complete the analysis response.");
            }

            if (root.TryGetProperty("error", out JsonElement error)
                && error.ValueKind != JsonValueKind.Null)
            {
                throw new OpenAiResponseException(
                    "OpenAI response reported an error.");
            }

            var output = getArray(root, "output");
            var outputText = new StringBuilder();

            foreach (var item in output.EnumerateArray())
            {
                if (getString(item, "type") != "message")
                {
                    continue;
                }

                var content = getArray(item, "content");

                foreach (var part in content.EnumerateArray())
                {
                    var contentType = getString(part, "type");

                    switch (contentType)
                    {
                        case "refusal":
                            throw new OpenAiResponseException(
                                "OpenAI refused to analyze the ticket.");
                        case "output_text":
                            outputText.Append(getString(part, "text"));
                            break;
                    }
                }
            }

            var analysisJson = outputText.ToString();

            if (string.IsNullOrWhiteSpace(analysisJson))
            {
                throw new OpenAiResponseException(
                    "OpenAI response did not contain analysis output.");
            }

            var result =
                JsonSerializer.Deserialize<TicketAiAnalysisResult>(
                    analysisJson,
                    ResultJsonOptions)
                ?? throw new OpenAiResponseException(
                    "OpenAI returned a null analysis.");

            if (result.TroubleshootingSuggestions.Any(item => item is null)
                || result.MissingInformation.Any(item => item is null))
            {
                throw new OpenAiResponseException(
                    "OpenAI analysis contained a null array item.");
            }

            return result;
        }
        catch (JsonException exception)
        {
            throw new OpenAiResponseException(
                "OpenAI returned malformed analysis JSON.",
                exception);
        }
    }

    private static string getString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            throw new OpenAiResponseException(
                $"OpenAI response has a missing or invalid '{propertyName}' field.");
        }

        return property.GetString()!;
    }

    private static JsonElement getArray(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.Array)
        {
            throw new OpenAiResponseException(
                $"OpenAI response has a missing or invalid '{propertyName}' field.");
        }

        return property;
    }
}