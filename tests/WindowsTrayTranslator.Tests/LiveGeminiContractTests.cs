using System.Text;
using System.Text.RegularExpressions;
using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Security;
using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

/// <summary>
/// Gate 3 (design 21) evidence. These call the real Gemini API with the user's stored key, so they
/// are skipped unless WTT_LIVE_GEMINI=1. Every run writes a per-iteration report under
/// artifacts/verification so the manual matrix can cite real numbers instead of a claim.
/// </summary>
public sealed class LiveGeminiContractTests
{
    private const string MarkerFragment = "WTT-LINE";

    [LiveGeminiFact]
    public Task G_RewriteNatural_MeetsOutputContract() =>
        RunContractAsync(BuiltInActionKind.RewriteNatural);

    [LiveGeminiFact]
    public Task G_Summarize_MeetsOutputContract() =>
        RunContractAsync(BuiltInActionKind.Summarize);

    /// <summary>
    /// Only the second step of the translate-then-summarize pipeline is measured here: the Korean
    /// sample is handed to the summary prompt with an English target, which is exactly the case
    /// where the model may drift back into the source language. The translation step itself is
    /// already covered by the translation contract.
    /// </summary>
    [LiveGeminiFact]
    public Task G_SummarizeTranslated_MeetsOutputContract() =>
        RunContractAsync(BuiltInActionKind.SummarizeTranslated, "English");

    private static async Task RunContractAsync(BuiltInActionKind kind, string? targetLanguage = null)
    {
        int iterations = ReadInt("WTT_LIVE_ITERATIONS", 20);
        double threshold = ReadInt("WTT_LIVE_THRESHOLD_PERCENT", 95) / 100.0;

        // Language preservation is an invariant, not a style preference: answering a Korean rewrite
        // request with an English translation is a wrong answer. A measured 19/20 is exactly 95%,
        // so the shared 95% bar would have let it through.
        double languageThreshold = ReadInt("WTT_LIVE_LANGUAGE_THRESHOLD_PERCENT", 100) / 100.0;
        string input = BuildSample(ReadInt("WTT_LIVE_INPUT_CHARS", 4500));

        // The sample is Korean, so the expected output language is the configured target when the
        // action declares one, and the source language otherwise.
        bool expectKorean = targetLanguage is null
            ? AutomaticTargetLanguageResolver.IsKoreanDominant(input)
            : string.Equals(targetLanguage, "Korean", StringComparison.OrdinalIgnoreCase);
        string languageLabel = targetLanguage is null ? "원문 언어 유지" : $"대상 언어({targetLanguage}) 준수";

        AppPaths paths = AppPaths.CreateDefault();
        TestLogger logger = new();
        ConfigurationService configuration = new(paths.SettingsFile, logger);
        AppSettings settings = configuration.Load();
        DpapiSecretStore secrets = new(paths.SecretFile);
        Assert.False(
            string.IsNullOrWhiteSpace(secrets.ReadSecret(SecretNames.GeminiApiKey)),
            "저장된 Gemini API 키가 없습니다. 앱 설정에서 키를 저장한 뒤 다시 실행하세요.");

        using HttpClient http = new();
        GeminiTranslationProvider provider = new(
            new GeminiApiClient(http, logger),
            secrets,
            () => settings);

        // Back-to-back requests hit 429 and silently shrink the sample, so pace them.
        int delayMs = ReadInt("WTT_LIVE_DELAY_MS", 1500);

        List<Observation> observations = [];
        for (int index = 1; index <= iterations; index++)
        {
            if (index > 1)
            {
                await Task.Delay(delayMs);
            }

            TranslationResult result = await provider.GenerateAsync(
                PromptComposer.Compose(kind, input, targetLanguage),
                CancellationToken.None);

            // One retry after a rate limit, so a throttled call does not cost a sample.
            if (!result.IsSuccess && result.FailureKind == TranslationFailureKind.RateLimit)
            {
                await Task.Delay(ReadInt("WTT_LIVE_RATELIMIT_BACKOFF_MS", 20000));
                result = await provider.GenerateAsync(
                    PromptComposer.Compose(kind, input, targetLanguage),
                    CancellationToken.None);
            }

            observations.Add(Observation.From(index, input, result, expectKorean));
        }

        string reportPath = WriteReport(
            kind,
            input,
            observations,
            GeminiTranslationProvider.ResolveActionModel(settings.Api),
            languageLabel);

        // A shrunken sample must not look like a pass: the rates below are computed over successes
        // only, so require most of the planned iterations to have produced a real answer.
        int succeeded = observations.Count(o => o.IsSuccess);
        Assert.True(
            succeeded >= Math.Max(5, (int)(iterations * 0.8)),
            $"성공 표본이 부족해 판정할 수 없습니다: {succeeded}/{iterations}. 보고서: {reportPath}");

        // G5 - a leaked internal marker is a defect we control, so it must never happen.
        Assert.Equal(0, observations.Count(o => o.HasMarker));

        AssertRate(observations, o => o.KeepsExpectedLanguage, languageThreshold, languageLabel, reportPath);
        AssertRate(observations, o => o.IsCleanText, threshold, "라벨/코드펜스 없음", reportPath);

        // Only the same-language summary has a meaningful character-count bar: a Korean source
        // summarized into English can grow per character even when it is a real summary.
        if (kind == BuiltInActionKind.Summarize)
        {
            AssertRate(observations, o => o.LengthRatio < 1.0, threshold, "요약이 원문보다 짧음", reportPath);
        }
    }

    private static void AssertRate(
        IReadOnlyList<Observation> observations,
        Func<Observation, bool> predicate,
        double threshold,
        string label,
        string reportPath)
    {
        Observation[] succeeded = observations.Where(o => o.IsSuccess).ToArray();
        int met = succeeded.Count(predicate);
        double rate = (double)met / succeeded.Length;
        Assert.True(
            rate >= threshold,
            $"{label}: {met}/{succeeded.Length} ({rate:P0}) < 기준 {threshold:P0}. 보고서: {reportPath}");
    }

    private static string WriteReport(
        BuiltInActionKind kind,
        string input,
        IReadOnlyList<Observation> observations,
        string model,
        string languageLabel)
    {
        string directory = Path.Combine(FindRepositoryRoot(), "artifacts", "verification");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(
            directory,
            $"gemini-contract-{kind}-{DateTime.Now:yyyyMMdd-HHmmss}.md");

        Observation[] succeeded = observations.Where(o => o.IsSuccess).ToArray();
        StringBuilder report = new();
        report.AppendLine($"# Gemini 출력 계약 실측 - {kind}");
        report.AppendLine();
        report.AppendLine($"- 실행 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"- 모델: {model}");
        report.AppendLine($"- 입력 길이: {input.Length}자");
        report.AppendLine($"- 반복: {observations.Count}회 (성공 {succeeded.Length}회)");
        report.AppendLine();
        report.AppendLine($"| # | 성공 | 출력자수 | 길이비 | {languageLabel} | 라벨/펜스없음 | marker |");
        report.AppendLine("|---|---|---|---|---|---|---|");
        foreach (Observation o in observations)
        {
            report.AppendLine(
                $"| {o.Index} | {(o.IsSuccess ? "O" : "X " + o.FailureKind)} | {o.OutputLength} | " +
                $"{o.LengthRatio:F2} | {Mark(o.KeepsExpectedLanguage)} | {Mark(o.IsCleanText)} | {(o.HasMarker ? "유출" : "없음")} |");
        }

        if (succeeded.Length > 0)
        {
            report.AppendLine();
            report.AppendLine("## 집계");
            report.AppendLine();
            report.AppendLine($"- {languageLabel}: {succeeded.Count(o => o.KeepsExpectedLanguage)}/{succeeded.Length}");
            report.AppendLine($"- 라벨/코드펜스 없음: {succeeded.Count(o => o.IsCleanText)}/{succeeded.Length}");
            report.AppendLine($"- marker 유출: {observations.Count(o => o.HasMarker)}회");
            report.AppendLine($"- 길이비 최소/중앙/최대: {succeeded.Min(o => o.LengthRatio):F2} / " +
                $"{succeeded.OrderBy(o => o.LengthRatio).ElementAt(succeeded.Length / 2).LengthRatio:F2} / " +
                $"{succeeded.Max(o => o.LengthRatio):F2}");
        }

        File.WriteAllText(path, report.ToString(), Encoding.UTF8);
        return path;
    }

    private static string Mark(bool value) => value ? "O" : "X";

    private static string BuildSample(int targetLength)
    {
        string[] sentences =
        [
            "이번 분기 검사 설비의 가동률은 목표치를 소폭 밑돌았으나 불량 유출은 전 분기 대비 감소했습니다.",
            "설비 담당자는 야간 교대 중 발생한 정지 이력을 매일 아침 회의에서 공유하기로 했습니다.",
            "신규 도입한 비전 검사 항목은 초기 오검출이 있었지만 임계값을 조정한 뒤 안정화되었습니다.",
            "협력사 입고 자재의 치수 산포가 커서 수입 검사 기준을 다시 검토할 필요가 있습니다.",
            "다음 달부터는 설비별 점검 주기를 표준화하고 점검 결과를 동일한 양식으로 기록합니다.",
            "현장에서 제기된 개선 요청 중 우선순위가 높은 세 건을 먼저 반영하기로 결정했습니다."
        ];

        StringBuilder builder = new();
        int index = 0;
        while (builder.Length < targetLength)
        {
            builder.Append(sentences[index % sentences.Length]).Append(' ');
            index++;
            if (index % sentences.Length == 0)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString(0, targetLength);
    }

    private static int ReadInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0 ? value : fallback;

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WindowsTrayTranslator.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }

    private sealed record Observation(
        int Index,
        bool IsSuccess,
        TranslationFailureKind FailureKind,
        int OutputLength,
        double LengthRatio,
        bool KeepsExpectedLanguage,
        bool IsCleanText,
        bool HasMarker)
    {
        public static Observation From(int index, string input, TranslationResult result, bool expectKorean)
        {
            if (!result.IsSuccess)
            {
                return new Observation(index, false, result.FailureKind, 0, 0, false, false, false);
            }

            string output = result.TranslatedText!;
            return new Observation(
                index,
                true,
                TranslationFailureKind.None,
                output.Length,
                (double)output.Length / input.Length,
                AutomaticTargetLanguageResolver.IsKoreanDominant(output) == expectKorean,
                IsCleanOutput(output),
                output.Contains(MarkerFragment, StringComparison.Ordinal));
        }

        private static bool IsCleanOutput(string output)
        {
            string trimmed = output.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.Contains("```", StringComparison.Ordinal))
            {
                return false;
            }

            // A leading "요약:" / "Summary:" style label is exactly what the prompt forbids.
            return !Regex.IsMatch(trimmed, @"^\s*(요약|정리|결과|다듬은 문장|Summary|Rewritten|Result)\s*[:：]");
        }
    }
}

/// <summary>Skipped unless WTT_LIVE_GEMINI=1, so a normal test run never calls the network.</summary>
public sealed class LiveGeminiFactAttribute : FactAttribute
{
    public LiveGeminiFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WTT_LIVE_GEMINI") != "1")
        {
            Skip = "WTT_LIVE_GEMINI=1 이 아니므로 건너뜁니다 (실제 Gemini 호출).";
        }
    }
}
