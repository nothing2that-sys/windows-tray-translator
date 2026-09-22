using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

public sealed class GeminiModelPolicyTests
{
    [Theory]
    [InlineData("gemini-1.5-flash")]
    [InlineData("gemini-2.0-flash")]
    public void EnsureUsable_KnownRetiredModel_Throws(string model)
    {
        Assert.Throws<InvalidOperationException>(() => GeminiModelPolicy.EnsureUsable(model));
    }

    [Theory]
    [InlineData("gemini-2.5-flash-lite")]
    [InlineData("gemini-3.5-flash-lite")]
    public void EnsureUsable_CurrentModel_DoesNotThrow(string model)
    {
        GeminiModelPolicy.EnsureUsable(model);
    }
}
