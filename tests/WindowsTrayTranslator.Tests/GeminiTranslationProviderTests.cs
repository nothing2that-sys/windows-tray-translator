using System.Net;
using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Security;
using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

public sealed class GeminiTranslationProviderTests
{
    [Fact]
    public async Task TranslateAsync_PrimaryRateLimited_UsesConfiguredFallbackModel()
    {
        List<string> models = [];
        HttpClient httpClient = new(new Handler(request =>
        {
            models.Add(request.RequestUri!.Segments[^1].Split(':')[0]);
            return models.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"안녕\"}]}}]}")
                };
        }));
        AppSettings settings = new();
        settings.Api.Model = "primary-model";
        settings.Api.EnableFallbackModel = true;
        settings.Api.FallbackModel = "fallback-model";
        settings.Api.TransientRetryCount = 0;
        GeminiTranslationProvider provider = new(
            new GeminiApiClient(httpClient, new TestLogger()),
            new FakeSecretStore(),
            () => settings);

        TranslationResult result = await provider.TranslateAsync(
            new TranslationRequest("Hello", "Korean"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["primary-model", "fallback-model"], models);
    }

    [Fact]
    public async Task TranslateWithModelAsync_UsesOnlyRequestedModel()
    {
        List<string> models = [];
        HttpClient httpClient = new(new Handler(request =>
        {
            models.Add(request.RequestUri!.Segments[^1].Split(':')[0]);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"다른 번역\"}]}}]}")
            };
        }));
        AppSettings settings = new();
        settings.Api.Model = "primary-model";
        settings.Api.EnableFallbackModel = true;
        settings.Api.FallbackModel = "fallback-model";
        GeminiTranslationProvider provider = new(
            new GeminiApiClient(httpClient, new TestLogger()),
            new FakeSecretStore(),
            () => settings);

        TranslationResult result = await provider.TranslateWithModelAsync(
            new TranslationRequest("Hello", "Korean"),
            "manually-selected-model",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("다른 번역", result.TranslatedText);
        Assert.Equal(["manually-selected-model"], models);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        public bool HasSecret(string key) => true;
        public string? ReadSecret(string key) => "key";
        public void WriteSecret(string key, string value) { }
        public void DeleteSecret(string key) { }
    }
}
