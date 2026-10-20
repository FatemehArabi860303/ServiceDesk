using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Options;
using ServiceDesk.AI.AiAnalysis;
using ServiceDesk.AI.Configuration;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Core.Tickets;

namespace ServiceDesk.AI.Tests.AiAnalysis;

public sealed class OpenAiTicketAnalyzerTests
{
    private const string ValidAnalysisJson = """
        {
          "summary": "The customer cannot connect to the VPN.",
          "suggestedCategory": "Connectivity",
          "suggestedPriority": "High",
          "priorityReason": "The issue prevents remote access.",
          "troubleshootingSuggestions": [
            "Check the network connection."
          ],
          "missingInformation": [
            "Which VPN client version is installed?"
          ],
          "confidence": "Medium"
        }
        """;

    [Fact]
    public async Task AnalyzeAsync_WithWorkerConfiguration_UsesSingleVersionSegmentInRequestUri()
    {
        // Arrange
        using JsonDocument configuration = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "AiWorker.appsettings.json")));
        string configuredBaseUrl = configuration.RootElement.GetProperty("OpenAI").GetProperty("BaseUrl").GetString()!;
        Uri? actualUri = null;
        using HttpClient client = createHttpClient((request, cancellationToken) =>
        {
            actualUri = request.RequestUri;
            return Task.FromResult(jsonResponse(completedResponse(ValidAnalysisJson)));
        });
        client.BaseAddress = new Uri(configuredBaseUrl, UriKind.Absolute);
        OpenAiOptions options = new() { ApiKey = "test-key", Model = "test-model", BaseUrl = configuredBaseUrl };
        OpenAiTicketAnalyzer analyzer = new(client, Options.Create(options));

        // Act
        await analyzer.AnalyzeAsync(createInput(), CancellationToken.None);

        // Assert
        actualUri.Should().Be(new Uri("https://api.openai.com/v1/responses"));
        actualUri!.AbsolutePath.Should().Be("/v1/responses");
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidResponse_SendsStructuredRequestAndReturnsResult()
    {
        // Arrange
        HttpMethod? actualMethod = null;
        Uri? actualUri = null;
        string? actualAuthorizationScheme = null;
        string? actualAuthorizationParameter = null;
        string? actualContentType = null;
        string? requestJson = null;

        HttpMethod expectedMethod = HttpMethod.Post;
        Uri expectedUri = new Uri("https://api.openai.com/v1/responses");

        TicketAiAnalysisResult expectedResult = createExpectedResult();

        string[] expectedRequiredFields =
        [
            "summary",
            "suggestedCategory",
            "suggestedPriority",
            "priorityReason",
            "troubleshootingSuggestions",
            "missingInformation",
            "confidence"
        ];

        string[] expectedPriorities = Enum.GetNames<TicketPriority>();

        using HttpResponseMessage response =
            jsonResponse(completedResponse(ValidAnalysisJson));

        using HttpClient client = createHttpClient(
            async (
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            {
                actualMethod = request.Method;
                actualUri = request.RequestUri;
                actualAuthorizationScheme =
                    request.Headers.Authorization?.Scheme;
                actualAuthorizationParameter =
                    request.Headers.Authorization?.Parameter;
                actualContentType =
                    request.Content?.Headers.ContentType?.MediaType;

                requestJson = await request.Content!.ReadAsStringAsync(
                    cancellationToken);

                return response;
            });

        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        string expectedTitle = input.Title;
        string expectedDescription = input.Description;

        // Act
        TicketAiAnalysisResult result = await analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        using JsonDocument requestDocument = JsonDocument.Parse(requestJson!);
        JsonElement body = requestDocument.RootElement;

        string? actualModel = body.GetProperty("model").GetString();
        string? actualInstructions = body.GetProperty("instructions").GetString();
        bool actualStore = body.GetProperty("store").GetBoolean();

        string inputJson = body.GetProperty("input").GetString()!;
        using JsonDocument inputDocument = JsonDocument.Parse(inputJson);
        JsonElement requestInput = inputDocument.RootElement;

        string? actualTitle = requestInput.GetProperty("title").GetString();
        string? actualDescription =
            requestInput.GetProperty("description").GetString();
        int actualInputPropertyCount = requestInput.EnumerateObject().Count();

        JsonElement format = body.GetProperty("text").GetProperty("format");
        string? actualFormatType = format.GetProperty("type").GetString();
        bool actualStrict = format.GetProperty("strict").GetBoolean();

        JsonElement schema = format.GetProperty("schema");
        bool actualAdditionalProperties =
            schema.GetProperty("additionalProperties").GetBoolean();

        string?[] actualRequiredFields = schema
            .GetProperty("required")
            .EnumerateArray()
            .Select((JsonElement item) => item.GetString())
            .ToArray();

        string?[] actualPriorities = schema
            .GetProperty("properties")
            .GetProperty("suggestedPriority")
            .GetProperty("enum")
            .EnumerateArray()
            .Select((JsonElement item) => item.GetString())
            .ToArray();

        // Assert
        result.Should().BeEquivalentTo(expectedResult);

        actualMethod.Should().Be(expectedMethod);
        actualUri.Should().Be(expectedUri);
        actualAuthorizationScheme.Should().Be("Bearer");
        actualAuthorizationParameter.Should().Be("test-key");
        actualContentType.Should().Be("application/json");

        actualModel.Should().Be("test-model");
        actualInstructions.Should().NotBeNullOrWhiteSpace();
        actualStore.Should().BeFalse();

        actualTitle.Should().Be(expectedTitle);
        actualDescription.Should().Be(expectedDescription);
        actualInputPropertyCount.Should().Be(2);

        actualFormatType.Should().Be("json_schema");
        actualStrict.Should().BeTrue();
        actualAdditionalProperties.Should().BeFalse();
        actualRequiredFields.Should().BeEquivalentTo(expectedRequiredFields);
        actualPriorities.Should().BeEquivalentTo(expectedPriorities);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AnalyzeAsync_WithHttpFailure_ThrowsWithStatusCode(
        HttpStatusCode statusCode)
    {
        // Arrange
        using HttpClient client = createHttpClient("{}", statusCode);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeAssignableTo<HttpRequestException>()
            .Which.StatusCode.Should().Be(statusCode);
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("failed")]
    public async Task AnalyzeAsync_WithUncompletedResponse_ThrowsProviderResponseException(
        string status)
    {
        // Arrange
        string responseJson = JsonSerializer.Serialize(new
        {
            status,
            output = Array.Empty<object>()
        });

        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeOfType<OpenAiResponseException>()
            .Which.Message.Should()
            .Be("OpenAI did not complete the analysis response.");
    }

    [Fact]
    public async Task AnalyzeAsync_WithRefusal_ThrowsProviderResponseException()
    {
        // Arrange
        const string responseJson = """
            {
              "status": "completed",
              "output": [
                {
                  "type": "message",
                  "content": [
                    {
                      "type": "refusal",
                      "refusal": "The request was refused."
                    }
                  ]
                }
              ]
            }
            """;

        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeOfType<OpenAiResponseException>()
            .Which.Message.Should()
            .Be("OpenAI refused to analyze the ticket.");
    }

    [Theory]
    [InlineData("{\"status\":\"completed\"}")]
    [InlineData("{\"status\":\"completed\",\"output\":[]}")]
    [InlineData("""
        {
          "status": "completed",
          "output": [
            {
              "type": "message",
              "content": [
                { "type": "output_text", "text": " " }
              ]
            }
          ]
        }
        """)]
    public async Task AnalyzeAsync_WithMissingOutput_ThrowsProviderResponseException(
        string responseJson)
    {
        // Arrange
        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeOfType<OpenAiResponseException>();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"status\":\"completed\",\"output\":{}}")]
    [InlineData("""
        {
          "status": "completed",
          "output": [
            { "type": "message", "content": {} }
          ]
        }
        """)]
    public async Task AnalyzeAsync_WithMalformedEnvelope_ThrowsProviderResponseException(
        string responseJson)
    {
        // Arrange
        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeOfType<OpenAiResponseException>();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task AnalyzeAsync_WithMalformedAnalysis_ThrowsProviderResponseException(
        string analysisJson)
    {
        // Arrange
        string responseJson = completedResponse(analysisJson);

        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeOfType<OpenAiResponseException>();
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("suggestedCategory")]
    [InlineData("suggestedPriority")]
    [InlineData("priorityReason")]
    [InlineData("troubleshootingSuggestions")]
    [InlineData("missingInformation")]
    [InlineData("confidence")]
    public async Task AnalyzeAsync_WithMissingAnalysisField_ThrowsProviderResponseException(
        string fieldName)
    {
        // Arrange
        JsonObject analysis = JsonNode.Parse(ValidAnalysisJson)!.AsObject();
        analysis.Remove(fieldName);

        string responseJson = completedResponse(analysis.ToJsonString());

        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeOfType<OpenAiResponseException>();
    }

    [Fact]
    public async Task AnalyzeAsync_WithNullArrayItem_ThrowsProviderResponseException()
    {
        // Arrange
        JsonObject analysis = JsonNode.Parse(ValidAnalysisJson)!.AsObject();
        analysis["missingInformation"] = JsonNode.Parse("[null]");

        string responseJson = completedResponse(analysis.ToJsonString());

        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeOfType<OpenAiResponseException>()
            .Which.Message.Should()
            .Be("OpenAI analysis contained a null array item.");
    }

    [Fact]
    public async Task AnalyzeAsync_WithNumericEnum_ThrowsProviderResponseException()
    {
        // Arrange
        JsonObject analysis = JsonNode.Parse(ValidAnalysisJson)!.AsObject();
        analysis["suggestedPriority"] = 2;

        string responseJson = completedResponse(analysis.ToJsonString());

        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeOfType<OpenAiResponseException>();
    }

    [Fact]
    public async Task AnalyzeAsync_WithBusinessInvalidResult_ReturnsDataForCoreValidation()
    {
        // Arrange
        JsonObject analysis = JsonNode.Parse(ValidAnalysisJson)!.AsObject();
        analysis["summary"] = "";

        string responseJson = completedResponse(analysis.ToJsonString());

        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        // Act
        TicketAiAnalysisResult result = await analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        // Assert
        result.Summary.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_WithParsedBlankSummary_ThrowsMissingSummary()
    {
        // Arrange
        JsonObject analysis = JsonNode.Parse(ValidAnalysisJson)!.AsObject();
        analysis["summary"] = "";

        string responseJson = completedResponse(analysis.ToJsonString());

        using HttpClient client = createHttpClient(responseJson);
        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        TicketAiAnalysisResult parsedResult = await analyzer.AnalyzeAsync(
            input,
            CancellationToken.None);

        Action act = () => ValidateTicketAiAnalysisCore.Execute(parsedResult);

        // Act
        Exception? exception = Record.Exception(act);

        // Assert
        exception.Should().BeOfType<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.MissingSummary);
    }

    [Fact]
    public async Task AnalyzeAsync_WithAlreadyCancelledToken_DoesNotSendRequest()
    {
        // Arrange
        bool requestSent = false;

        using HttpResponseMessage response =
            jsonResponse(completedResponse(ValidAnalysisJson));

        using HttpClient client = createHttpClient(
            (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                requestSent = true;
                return Task.FromResult(response);
            });

        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        using CancellationTokenSource cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        Func<Task> act = () => analyzer.AnalyzeAsync(
            input,
            cancellation.Token);

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeAssignableTo<OperationCanceledException>();
        requestSent.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_WithCancellationDuringHttpRequest_PropagatesCancellation()
    {
        // Arrange
        bool httpTokenCanBeCanceled = false;

        TaskCompletionSource requestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        TimeSpan testTimeout = TimeSpan.FromSeconds(5);

        using HttpResponseMessage response =
            jsonResponse(completedResponse(ValidAnalysisJson));

        using HttpClient client = createHttpClient(
            async (
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            {
                httpTokenCanBeCanceled = cancellationToken.CanBeCanceled;
                requestStarted.SetResult();

                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);

                return response;
            });

        OpenAiTicketAnalyzer analyzer = createAnalyzer(client);
        TicketAiAnalysisInput input = createInput();

        using CancellationTokenSource cancellation =
            new CancellationTokenSource();

        Func<Task> act = async () =>
        {
            Task<TicketAiAnalysisResult> operation = analyzer.AnalyzeAsync(
                input,
                cancellation.Token);

            await requestStarted.Task.WaitAsync(testTimeout);
            cancellation.Cancel();

            await operation.WaitAsync(testTimeout);
        };

        // Act
        Exception? exception = await Record.ExceptionAsync(act);

        // Assert
        exception.Should().BeAssignableTo<OperationCanceledException>();
        httpTokenCanBeCanceled.Should().BeTrue();
    }

    private static TicketAiAnalysisInput createInput()
    {
        return new TicketAiAnalysisInput(
            Guid.Parse("bfe9a994-1e86-42b9-a429-ef131d29de77"),
            "VPN connection failure",
            "The VPN client fails to connect from my home network.");
    }

    private static TicketAiAnalysisResult createExpectedResult()
    {
        return new TicketAiAnalysisResult(
            Summary: "The customer cannot connect to the VPN.",
            SuggestedCategory: "Connectivity",
            SuggestedPriority: TicketPriority.High,
            PriorityReason: "The issue prevents remote access.",
            TroubleshootingSuggestions: ["Check the network connection."],
            MissingInformation: ["Which VPN client version is installed?"],
            Confidence: TicketAiAnalysisConfidence.Medium);
    }

    private static OpenAiTicketAnalyzer createAnalyzer(HttpClient client)
    {
        OpenAiOptions options = new OpenAiOptions
        {
            ApiKey = "test-key",
            Model = "test-model",
            BaseUrl = "https://api.openai.com/"
        };

        IOptions<OpenAiOptions> configuredOptions = Options.Create(options);

        return new OpenAiTicketAnalyzer(client, configuredOptions);
    }

    private static string completedResponse(string analysisJson)
    {
        return JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new object[]
            {
                new { type = "reasoning" },
                new
                {
                    type = "message",
                    content = new[]
                    {
                        new { type = "output_text", text = analysisJson }
                    }
                }
            }
        });
    }

    private static HttpClient createHttpClient(
        string responseJson,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        HttpResponseMessage response = jsonResponse(responseJson, statusCode);

        return createHttpClient(
            (HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(response));
    }

    private static HttpClient createHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    {
        StubHttpMessageHandler handler = new StubHttpMessageHandler(send);

        HttpClient client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/")
        };

        return client;
    }

    private static HttpResponseMessage jsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        StringContent content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = new HttpResponseMessage(statusCode)
        {
            Content = content
        };

        return response;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return send(request, cancellationToken);
        }
    }
}
