using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class PdfUnlockPolicyTests
{
    [Fact]
    public void Password_prompt_exists_for_view_unlock_only()
    {
        Assert.True(PdfUnlockPolicy.PromptExists);
        Assert.True(PdfUnlockPolicy.ViewUnlockOnly);
        Assert.True(PdfUnlockPolicy.IsViewUnlockPurpose("view"));
        Assert.True(PdfUnlockPolicy.IsViewUnlockPurpose("VIEW"));
        Assert.False(PdfUnlockPolicy.IsViewUnlockPurpose("edit"));
        Assert.False(PdfUnlockPolicy.IsViewUnlockPurpose("drm"));
    }

    [Fact]
    public void Password_is_never_persisted()
    {
        Assert.False(PdfUnlockPolicy.ShouldPersistPassword);
    }

    [Fact]
    public void Wrong_password_prompts_again()
    {
        Assert.True(PdfUnlockPolicy.ShouldPromptAgainAfterFailure);
        Assert.True(PdfUnlockPolicy.ShouldPrompt(requiresPassword: true, suppliedPassword: null));
        Assert.True(PdfUnlockPolicy.ShouldPrompt(requiresPassword: true, suppliedPassword: ""));
        Assert.False(PdfUnlockPolicy.ShouldPrompt(requiresPassword: true, suppliedPassword: "secret"));
        Assert.False(PdfUnlockPolicy.ShouldPrompt(requiresPassword: false, suppliedPassword: null));
    }
}
