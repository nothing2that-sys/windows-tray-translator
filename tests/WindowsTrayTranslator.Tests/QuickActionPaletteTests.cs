using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Tests;

public sealed class QuickActionPaletteTests
{
    private static readonly WindowIdentity Window = new(new IntPtr(1), 2, "test", new IntPtr(3));

    private static QuickActionSession NewSession(string text = "선택한 문장") =>
        new(new CapturedSelection(text, Window));

    [Fact]
    public void Session_CompletesOnlyOnce()
    {
        QuickActionSession session = NewSession();
        int completing = 0;
        session.Completing += (_, _) => completing++;

        Assert.True(session.TryComplete());
        Assert.False(session.TryComplete());
        Assert.False(session.TryComplete());

        Assert.True(session.Completed);
        Assert.Equal(1, completing);
    }

    [Fact]
    public void Admission_ClaimsGateAndDoesNotReleaseItOnStart()
    {
        QuickActionSession session = NewSession();
        GateSpy gate = new(available: true);

        PaletteAdmission admission = ApplicationController.DecidePaletteAdmission(
            session, isCurrentSession: true, gate.TryAcquire, gate.Release, () => true);

        Assert.Equal(PaletteAdmission.Started, admission);
        Assert.Equal(1, gate.AcquireCount);
        Assert.Equal(0, gate.ReleaseCount);
        // The caller completes the session after registering the operation id.
        Assert.False(session.Completed);
    }

    [Fact]
    public void Admission_BusyGateKeepsSessionAndCapturedText()
    {
        QuickActionSession session = NewSession("보존되어야 하는 문장");
        GateSpy gate = new(available: false);

        PaletteAdmission admission = ApplicationController.DecidePaletteAdmission(
            session, isCurrentSession: true, gate.TryAcquire, gate.Release, () => true);

        Assert.Equal(PaletteAdmission.Busy, admission);
        Assert.False(session.Completed);
        Assert.Equal("보존되어야 하는 문장", session.Selection.Text);
        Assert.Equal(0, gate.ReleaseCount);
    }

    [Fact]
    public void Admission_TargetChangedReleasesGateAndBlocksGeneration()
    {
        QuickActionSession session = NewSession();
        GateSpy gate = new(available: true);

        PaletteAdmission admission = ApplicationController.DecidePaletteAdmission(
            session, isCurrentSession: true, gate.TryAcquire, gate.Release, () => false);

        Assert.Equal(PaletteAdmission.TargetChanged, admission);
        Assert.Equal(1, gate.AcquireCount);
        Assert.Equal(1, gate.ReleaseCount);
        Assert.True(session.Completed);
    }

    [Fact]
    public void Admission_StaleSessionNeverTouchesGate()
    {
        QuickActionSession replaced = NewSession();
        GateSpy gate = new(available: true);

        Assert.Equal(
            PaletteAdmission.Stale,
            ApplicationController.DecidePaletteAdmission(
                replaced, isCurrentSession: false, gate.TryAcquire, gate.Release, () => true));

        QuickActionSession finished = NewSession();
        finished.TryComplete();
        Assert.Equal(
            PaletteAdmission.Stale,
            ApplicationController.DecidePaletteAdmission(
                finished, isCurrentSession: true, gate.TryAcquire, gate.Release, () => true));

        Assert.Equal(0, gate.AcquireCount);
        Assert.Equal(0, gate.ReleaseCount);
    }

    [Fact]
    public void Admission_AfterTimeoutCompletesSession_ClickIsRejected()
    {
        QuickActionSession session = NewSession();
        GateSpy gate = new(available: true);

        // Timeout path completes the session first.
        Assert.True(session.TryComplete());

        Assert.Equal(
            PaletteAdmission.Stale,
            ApplicationController.DecidePaletteAdmission(
                session, isCurrentSession: true, gate.TryAcquire, gate.Release, () => true));
        Assert.Equal(0, gate.AcquireCount);
    }

    [Fact]
    public void ActionPopup_NormalResultIsSuccessAndCopyable()
    {
        AppSettings settings = new();
        (TranslationPopupKind kind, string message, string? copyText) =
            ApplicationController.ResolveActionPopup(TranslationResult.Success("다듬어진 문장"), settings);

        Assert.Equal(TranslationPopupKind.Success, kind);
        Assert.Equal("다듬어진 문장", message);
        Assert.Equal("다듬어진 문장", copyText);
    }

    [Fact]
    public void ActionPopup_OversizedResultWarnsButNeverFailsAndStaysCopyable()
    {
        AppSettings settings = new();
        settings.Translation.MaxInputCharacters = 10;
        string output = new('가', 40);

        (TranslationPopupKind kind, string message, string? copyText) =
            ApplicationController.ResolveActionPopup(TranslationResult.Success(output), settings);

        Assert.Equal(TranslationPopupKind.Warning, kind);
        Assert.NotEqual(TranslationPopupKind.Error, kind);
        Assert.Contains(output, message, StringComparison.Ordinal);
        Assert.Equal(output, copyText);
    }

    [Fact]
    public void ActionPopup_FailureIsErrorWithoutCopyText()
    {
        AppSettings settings = new();
        (TranslationPopupKind kind, string message, string? copyText) =
            ApplicationController.ResolveActionPopup(
                TranslationResult.Failure(TranslationFailureKind.Server, "서버에서 오류가 발생했습니다."),
                settings);

        Assert.Equal(TranslationPopupKind.Error, kind);
        Assert.Equal("서버에서 오류가 발생했습니다.", message);
        Assert.Null(copyText);
        Assert.DoesNotContain("번역", message);
    }

    [Fact]
    public void DisplayName_CoversEveryBuiltInAction()
    {
        foreach (BuiltInActionKind kind in Enum.GetValues<BuiltInActionKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(PromptComposer.DisplayName(kind)));
        }

        Assert.Equal("자연스럽게 다듬기", PromptComposer.DisplayName(BuiltInActionKind.RewriteNatural));
        Assert.Equal("요약", PromptComposer.DisplayName(BuiltInActionKind.Summarize));
        Assert.Equal("번역 후 요약", PromptComposer.DisplayName(BuiltInActionKind.SummarizeTranslated));
        Assert.Equal(3, Enum.GetValues<BuiltInActionKind>().Length);
    }

    /// <summary>
    /// The only pair that may skip the translation step is Korean source with a Korean target: any
    /// other combination must be translated, because only Korean is detectable here.
    /// </summary>
    [Fact]
    public void TranslatedSummary_SkipsOnlyTheKoreanToKoreanRoundTrip()
    {
        Assert.False(ApplicationController.ShouldTranslateBeforeSummary("Korean", "이번 분기 가동률은 목표치를 밑돌았습니다."));
        Assert.True(ApplicationController.ShouldTranslateBeforeSummary("English", "이번 분기 가동률은 목표치를 밑돌았습니다."));
        Assert.True(ApplicationController.ShouldTranslateBeforeSummary("Korean", "The utilization rate missed the target this quarter."));
        Assert.True(ApplicationController.ShouldTranslateBeforeSummary("Japanese", "稼働率は目標を下回りました。"));
    }

    /// <summary>
    /// "Select" would be sent to the API verbatim if it leaked through, so it must resolve to a real
    /// language without opening a second chooser during a palette action.
    /// </summary>
    [Fact]
    public void TranslatedSummary_ResolvesTheReadLanguageIncludingSelectAndAuto()
    {
        AppSettings settings = new();
        settings.Translation.ReadTargetLanguage = "Japanese";
        Assert.Equal("Japanese", ApplicationController.ResolveSummaryTargetLanguage(settings, "any text"));

        settings.Translation.ReadTargetLanguage = "Auto";
        Assert.Equal("English", ApplicationController.ResolveSummaryTargetLanguage(settings, "\uc774\ubc88 \ubd84\uae30 \uac00\ub3d9\ub960\uc740 \ubaa9\ud45c\uce58\ub97c \ubc11\ub3cc\uc558\uc2b5\ub2c8\ub2e4."));
        Assert.Equal("Korean", ApplicationController.ResolveSummaryTargetLanguage(settings, "The utilization rate missed the target."));

        settings.Translation.ReadTargetLanguage = "Select";
        settings.Translation.ReadRecentTargetLanguage = "Vietnamese";
        Assert.Equal("Vietnamese", ApplicationController.ResolveSummaryTargetLanguage(settings, "any text"));

        settings.Translation.ReadRecentTargetLanguage = "Select";
        Assert.Equal("Korean", ApplicationController.ResolveSummaryTargetLanguage(settings, "any text"));
    }

    private sealed class GateSpy(bool available)
    {
        public int AcquireCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public bool TryAcquire()
        {
            if (!available)
            {
                return false;
            }

            AcquireCount++;
            return true;
        }

        public void Release() => ReleaseCount++;
    }
}
