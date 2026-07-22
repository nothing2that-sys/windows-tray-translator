using System.Net;
using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

public sealed class GeminiApiClientTests
{
    [Fact]
    public async Task TranslateAsync_SendsKeyInHeaderNotUrl()
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"안녕\"}]}}]}")
        });
        GeminiApiClient client = new(new HttpClient(handler), new TestLogger());

        TranslationResult result = await client.TranslateAsync(
            "secret-key",
            "gemini-test",
            new TranslationRequest("Hello", "Korean"),
            10,
            0,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("secret-key", handler.LastRequest!.Headers.GetValues("x-goog-api-key").Single());
        Assert.DoesNotContain("secret-key", handler.LastRequest.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TranslationFailureKind.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, TranslationFailureKind.Authentication)]
    [InlineData(HttpStatusCode.TooManyRequests, TranslationFailureKind.RateLimit)]
    [InlineData(HttpStatusCode.InternalServerError, TranslationFailureKind.Server)]
    [InlineData(HttpStatusCode.BadRequest, TranslationFailureKind.InvalidRequest)]
    public async Task TranslateAsync_HttpFailure_ClassifiesStatus(HttpStatusCode status, TranslationFailureKind expected)
    {
        GeminiApiClient client = new(
            new HttpClient(new RecordingHandler(_ => new HttpResponseMessage(status))),
            new TestLogger());

        TranslationResult result = await client.TranslateAsync(
            "secret-key", "gemini-test", new TranslationRequest("Hello", "Korean"),
            10, 0, CancellationToken.None);

        Assert.Equal(expected, result.FailureKind);
    }

    [Fact]
    public async Task TranslateAsync_TransientStatus_RetriesWithinLimit()
    {
        int calls = 0;
        RecordingHandler handler = new(_ =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"안녕\"}]}}]}")
                };
        });

        TranslationResult result = await new GeminiApiClient(new HttpClient(handler), new TestLogger())
            .TranslateAsync("key", "model", new TranslationRequest("Hello", "Korean"), 10, 1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, calls);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}
