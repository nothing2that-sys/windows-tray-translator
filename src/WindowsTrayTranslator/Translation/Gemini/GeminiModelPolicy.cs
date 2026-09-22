namespace WindowsTrayTranslator.Translation.Gemini;

public static class GeminiModelPolicy
{
    private static readonly string[] RetiredPrefixes = ["gemini-1.5-", "gemini-2.0-"];

    public static bool IsKnownRetired(string? model) =>
        !string.IsNullOrWhiteSpace(model) &&
        RetiredPrefixes.Any(prefix => model.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public static void EnsureUsable(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Gemini 모델명을 입력해 주세요.");
        }
        if (IsKnownRetired(model))
        {
            throw new InvalidOperationException("종료된 Gemini 모델입니다. 모델 목록을 새로고침해 현재 모델을 선택해 주세요.");
        }
    }
}

public sealed record GeminiModelListResult(bool IsSuccess, IReadOnlyList<string> Models, string? ErrorMessage)
{
    public static GeminiModelListResult Success(IReadOnlyList<string> models) => new(true, models, null);
    public static GeminiModelListResult Failure(string message) => new(false, [], message);
}
