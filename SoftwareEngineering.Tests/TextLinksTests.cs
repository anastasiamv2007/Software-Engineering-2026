using SoftwareEngineering.BLL.Services;

namespace SoftwareEngineering.Tests;

public class TextLinksTests
{
    [Fact]
    public void Split_TextWithoutLinks_ReturnsSingleTextSegment()
    {
        // Act
        var segments = TextLinks.Split("Звичайний опис без посилань");

        // Assert
        var segment = Assert.Single(segments);
        Assert.Null(segment.Link);
        Assert.Equal("Звичайний опис без посилань", segment.Text);
    }

    [Fact]
    public void Split_EmptyOrNull_ReturnsNothing()
    {
        Assert.Empty(TextLinks.Split(null));
        Assert.Empty(TextLinks.Split(string.Empty));
    }

    [Fact]
    public void Split_LinkInMiddle_KeepsTextAroundIt()
    {
        // Act
        var segments = TextLinks.Split("Макет: https://figma.com/file/abc і далі текст");

        // Assert
        Assert.Equal(3, segments.Count);
        Assert.Equal("Макет: ", segments[0].Text);
        Assert.Equal("https://figma.com/file/abc", segments[1].Link!.AbsoluteUri);
        Assert.Equal(" і далі текст", segments[2].Text);
    }

    // Склеєні сегменти дають рівно вихідний текст — нічого не губиться і не дублюється
    [Fact]
    public void Split_JoinedSegments_EqualOriginalText()
    {
        // Arrange
        const string text = "Див. https://a.com, www.b.org.\nІ ще (https://c.net/x).";

        // Act
        var joined = string.Concat(TextLinks.Split(text).Select(segment => segment.Text));

        // Assert
        Assert.Equal(text, joined);
    }

    [Theory]
    [InlineData("Див. https://site.com.", "https://site.com/")]
    [InlineData("посилання https://site.com/page, далі", "https://site.com/page")]
    [InlineData("(https://site.com/a)", "https://site.com/a")]
    [InlineData("https://uk.wikipedia.org/wiki/C_(мова)", "https://uk.wikipedia.org/wiki/C_(%D0%BC%D0%BE%D0%B2%D0%B0)")]
    public void Split_TrailingPunctuation_IsNotPartOfLink(string text, string expected)
    {
        // Act
        var link = TextLinks.Split(text).Single(segment => segment.Link != null).Link!;

        // Assert
        Assert.Equal(expected, link.AbsoluteUri);
    }

    [Fact]
    public void Split_WwwWithoutScheme_OpensAsHttps()
    {
        // Act
        var link = TextLinks.Split("сайт www.lnu.edu.ua").Single(segment => segment.Link != null);

        // Assert
        Assert.Equal("www.lnu.edu.ua", link.Text);
        Assert.Equal("https", link.Link!.Scheme);
    }

    // Небезпечні схеми не стають посиланнями: клік не повинен запускати файл чи скрипт
    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://server/file")]
    public void Split_NonHttpSchemes_StayText(string text)
    {
        // Act
        var segments = TextLinks.Split(text);

        // Assert
        Assert.All(segments, segment => Assert.Null(segment.Link));
    }

    [Fact]
    public void Split_SeveralLinks_FindsEach()
    {
        // Act
        var links = TextLinks.Split("https://a.com та https://b.com/x?y=1").Where(s => s.Link != null).ToList();

        // Assert
        Assert.Equal(2, links.Count);
        Assert.Equal("https://b.com/x?y=1", links[1].Link!.AbsoluteUri);
    }
}
