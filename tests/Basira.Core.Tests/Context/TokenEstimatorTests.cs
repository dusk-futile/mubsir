using Basira.Core.Context;

namespace Basira.Core.Tests.Context;

public class TokenEstimatorTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abcd", 1)]
    [InlineData("abcde", 2)]
    [InlineData("مرحبا", 2)]   // 5 / 2.5
    [InlineData("مرحباً!", 3)] // 6 Arabic chars incl. tanween (2.4) + "!" (0.33) -> 3
    [InlineData("123", 1)]
    [InlineData("1234", 2)]
    [InlineData("١٢٣", 1)]
    [InlineData("😀", 1)]
    public void Estimates(string text, int expected)
    {
        Assert.Equal(expected, TokenEstimator.Estimate(text));
    }

    [Fact]
    public void MixedTextAddsCategoriesBeforeRoundingUp()
    {
        // 4 Latin + 1 space = 5 * 1/4, 5 Arabic * 1/2.5 -> 1.25 + 2 = 3.25 -> 4
        Assert.Equal(4, TokenEstimator.Estimate("abcd مرحبا"));
    }

    [Fact]
    public void ArabicCostsMoreThanLatinPerCharacter()
    {
        var arabic = new string('م', 100);
        var latin = new string('m', 100);
        Assert.Equal(40, TokenEstimator.Estimate(arabic));
        Assert.Equal(25, TokenEstimator.Estimate(latin));
    }

    [Fact]
    public void PrefixLengthWithinRespectsBudget()
    {
        var text = new string('a', 100);
        Assert.Equal(40, TokenEstimator.PrefixLengthWithin(text, 10));
        Assert.Equal(100, TokenEstimator.PrefixLengthWithin(text, 1000));
        Assert.Equal(0, TokenEstimator.PrefixLengthWithin(text, 0));
    }

    [Fact]
    public void PrefixNeverSplitsSurrogatePairs()
    {
        var text = "😀😀😀";
        int len = TokenEstimator.PrefixLengthWithin(text, 2);
        Assert.Equal(4, len);
        Assert.False(char.IsHighSurrogate(text[len - 1]));
    }
}
