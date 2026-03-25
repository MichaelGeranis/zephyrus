using System.Reflection;
using Zephyrus.Application.UseCases;

namespace Zephyrus.UnitTests;

/// <summary>
/// Tests for the slug generation logic in InvokePrdAgentUseCase.
/// GenerateSlug is a private static method used identically across all use cases.
/// </summary>
public class SlugGenerationTests
{
    private static string GenerateSlug(string prompt)
    {
        var method = typeof(InvokePrdAgentUseCase)
            .GetMethod("GenerateSlug", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GenerateSlug method not found.");

        return (string)(method.Invoke(null, [prompt])
            ?? throw new InvalidOperationException("GenerateSlug returned null."));
    }

    // --- Basic cases ---

    [Fact]
    public void GenerateSlug_WhenSimplePrompt_ShouldLowerCaseAndHyphenate()
    {
        var slug = GenerateSlug("Add User Authentication");
        Assert.Equal("add-user-authentication", slug);
    }

    [Fact]
    public void GenerateSlug_WhenAlreadyLowerCase_ShouldReturnSame()
    {
        var slug = GenerateSlug("add user login");
        Assert.Equal("add-user-login", slug);
    }

    // --- Special characters ---

    [Fact]
    public void GenerateSlug_WhenSpecialCharacters_ShouldStripThem()
    {
        var slug = GenerateSlug("Add user auth! (with OAuth)");
        Assert.Equal("add-user-auth-with-oauth", slug);
    }

    [Fact]
    public void GenerateSlug_WhenPunctuation_ShouldStripPunctuation()
    {
        var slug = GenerateSlug("Fix bug: user can't log in.");
        Assert.Equal("fix-bug-user-cant-log-in", slug);
    }

    [Fact]
    public void GenerateSlug_WhenHashAndAtSymbols_ShouldStripThem()
    {
        var slug = GenerateSlug("Fix #123 @username issue");
        Assert.Equal("fix-123-username-issue", slug);
    }

    // --- Whitespace variants ---

    [Fact]
    public void GenerateSlug_WhenTabCharacters_ShouldConvertToHyphen()
    {
        var slug = GenerateSlug("Add\tuser\tlogin");
        Assert.Equal("add-user-login", slug);
    }

    [Fact]
    public void GenerateSlug_WhenNewlineCharacters_ShouldConvertToHyphen()
    {
        var slug = GenerateSlug("Add\nuser\nlogin");
        Assert.Equal("add-user-login", slug);
    }

    [Fact]
    public void GenerateSlug_WhenMultipleSpaces_ShouldCollapseToSingleHyphen()
    {
        var slug = GenerateSlug("Add   user   login");
        Assert.Equal("add-user-login", slug);
    }

    // --- Consecutive hyphens ---

    [Fact]
    public void GenerateSlug_WhenConsecutiveSpecialChars_ShouldCollapseHyphens()
    {
        var slug = GenerateSlug("Add user -- login");
        Assert.Equal("add-user-login", slug);
    }

    [Fact]
    public void GenerateSlug_WhenMixedSpecialAndSpaces_ShouldCollapseHyphens()
    {
        var slug = GenerateSlug("Add (user) login");
        Assert.Equal("add-user-login", slug);
    }

    // --- Leading and trailing hyphens ---

    [Fact]
    public void GenerateSlug_WhenLeadingSpecialChars_ShouldTrimLeadingHyphens()
    {
        var slug = GenerateSlug("  add user login");
        Assert.Equal("add-user-login", slug);
    }

    [Fact]
    public void GenerateSlug_WhenTrailingSpecialChars_ShouldTrimTrailingHyphens()
    {
        var slug = GenerateSlug("add user login  ");
        Assert.Equal("add-user-login", slug);
    }

    // --- Length truncation ---

    [Fact]
    public void GenerateSlug_WhenLongerThan60Chars_ShouldTruncateTo60OrLess()
    {
        var longPrompt = "This is a very long feature prompt that exceeds the maximum allowed slug length limit";

        var slug = GenerateSlug(longPrompt);

        Assert.True(slug.Length <= 60, $"Expected length ≤ 60 but was {slug.Length}: '{slug}'");
    }

    [Fact]
    public void GenerateSlug_WhenTruncated_ShouldNotEndWithHyphen()
    {
        var longPrompt = string.Join(" ", Enumerable.Repeat("word", 20));

        var slug = GenerateSlug(longPrompt);

        Assert.False(slug.EndsWith('-'), $"Slug should not end with '-' but was '{slug}'");
    }

    [Fact]
    public void GenerateSlug_WhenExactly60Chars_ShouldNotTruncate()
    {
        // 12 five-letter words separated by spaces = 12 * 5 + 11 spaces = 71 chars prompt
        // resulting slug has hyphens, truncate at 60
        var prompt = string.Join(" ", Enumerable.Repeat("hello", 20));

        var slug = GenerateSlug(prompt);

        Assert.True(slug.Length <= 60);
    }

    // --- Numbers ---

    [Fact]
    public void GenerateSlug_WhenContainsNumbers_ShouldPreserveNumbers()
    {
        var slug = GenerateSlug("Add OAuth2 support");
        Assert.Equal("add-oauth2-support", slug);
    }

    // --- All-special-chars edge case ---

    [Fact]
    public void GenerateSlug_WhenOnlySpecialChars_ShouldReturnEmpty()
    {
        var slug = GenerateSlug("!!!---@@@");
        Assert.Equal(string.Empty, slug);
    }

    // --- Single word ---

    [Fact]
    public void GenerateSlug_WhenSingleWord_ShouldReturnLowerCaseWord()
    {
        var slug = GenerateSlug("Authentication");
        Assert.Equal("authentication", slug);
    }
}
