using Fidelizar.Domain.Money;

namespace Fidelizar.Domain.Tests.Money;

/// <summary>
/// The only place in the whole solution that parses money out of text, so the case table
/// deliberately covers the formats a hand-run export can throw at it: decimal comma, decimal dot,
/// a thousands separator of either kind, and the two genuinely ambiguous cases where guessing
/// would mean gambling with a member's money (I5).
///
/// Ported from Octaviano's <c>MontoParserTests</c> unchanged in coverage (F0-04) — every case
/// from the original suite is here, plus a couple of additional edge cases noted inline.
/// </summary>
public class MontoParserTests
{
    [Theory]
    [InlineData("1234.50", 1234.50)]
    [InlineData("1234,50", 1234.50)] // the bug: this used to give 123450 with NumberStyles.Any.
    [InlineData("1.234,50", 1234.50)]
    [InlineData("1,234.50", 1234.50)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("1234", 1234)]
    [InlineData("1234,5", 1234.5)]
    [InlineData("-500,25", -500.25)]
    [InlineData("$1.234,50", 1234.50)]
    public void TryParseMonto_ValidCases_ReturnsExpectedValue(string text, decimal expected)
    {
        Assert.True(MontoParser.TryParseMonto(text, out var monto));
        Assert.Equal(expected, monto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("loose text")]
    public void TryParseMonto_InvalidText_ReturnsFalseAndNotAmbiguous(string text)
    {
        Assert.False(MontoParser.TryParseMonto(text, out _, out var ambiguous));
        Assert.False(ambiguous);
    }

    [Theory]
    [InlineData("1,234")] // one thousand two hundred thirty-four, or a thousands comma?
    [InlineData("1.234")] // same dilemma with the separator reversed.
    public void TryParseMonto_SingleSeparatorWithThreeDigits_IsAmbiguousAndReturnsFalse(string text)
    {
        Assert.False(MontoParser.TryParseMonto(text, out var monto, out var ambiguous));
        Assert.Equal(0m, monto);
        Assert.True(ambiguous);
    }

    [Fact]
    public void TryParseMonto_OverloadWithoutAmbiguousFlag_AlsoReturnsFalseForTheAmbiguousCase()
    {
        Assert.False(MontoParser.TryParseMonto("1,234", out _));
    }

    /// <summary>
    /// Hand-typed mode. Shares with export mode the rule "the number of digits to the right
    /// decides whether it is decimal" — "1234.50" has to keep giving 1234.50, whatever the
    /// separator, because breaking that would just move the old <c>NumberStyles.Any</c> bug
    /// elsewhere instead of fixing it. It only differs from export mode in the exactly-3-digits
    /// case (the River Plate convention: dot thousands, comma decimal), which there is broken
    /// instead of rejected as ambiguous.
    /// </summary>
    [Theory]
    [InlineData("1234.50", 1234.50)] // the regression that crept in once: decimal dot, NOT thousands.
    [InlineData("1234,50", 1234.50)]
    [InlineData("1.2345", 1.2345)] // 4 digits to the right: unambiguously decimal.
    [InlineData("1.234,50", 1234.50)]
    [InlineData("1.500", 1500)] // 3 digits: River Plate tie-break, thousands dot.
    [InlineData("1,500", 1.5)] // 3 digits: River Plate tie-break, decimal comma.
    [InlineData("1500", 1500)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("-500,25", -500.25)]
    [InlineData("$1.500", 1500)]
    public void TryParseMontoManual_ValidCases_ReturnsExpectedValue(string text, decimal expected)
    {
        Assert.True(MontoParser.TryParseMontoManual(text, out var monto));
        Assert.Equal(expected, monto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("loose text")]
    public void TryParseMontoManual_InvalidText_ReturnsFalse(string text)
    {
        Assert.False(MontoParser.TryParseMontoManual(text, out _));
    }

    /// <summary>
    /// The case export mode declares ambiguous and rejects is, in hand-typed mode, exactly where
    /// the two modes have to give different results: there we do know a person is typing in the
    /// River Plate convention, so it gets resolved instead of rejected.
    /// </summary>
    [Theory]
    [InlineData("1,234", 1.234)]
    [InlineData("1.234", 1234)]
    public void TryParseMontoManual_DiffersFromExportMode_ExactlyOnTheCaseThatIsAmbiguousThere(
        string text, decimal expectedManual)
    {
        Assert.False(MontoParser.TryParseMonto(text, out _, out var ambiguous));
        Assert.True(ambiguous);

        Assert.True(MontoParser.TryParseMontoManual(text, out var monto));
        Assert.Equal(expectedManual, monto);
    }

    // --- Additional cases not present in the original Octaviano suite (F0-04: "if you find
    // obvious gaps, add cases, but don't drop any") ---

    [Theory]
    [InlineData("abc")]
    [InlineData("12a4")]
    [InlineData("1-234")] // a minus sign anywhere other than the very start is not a valid amount.
    public void TryParseMonto_TextWithNonNumericCharacters_ReturnsFalse(string text)
    {
        Assert.False(MontoParser.TryParseMonto(text, out _, out var ambiguous));
        Assert.False(ambiguous);
    }

    [Fact]
    public void TryParseMonto_HardSpaceAroundText_IsStrippedLikeAnyOtherWhitespace()
    {
        // U+00A0, a non-breaking space some spreadsheet exports use as a thousands separator or
        // padding — the parser must strip it the same as a regular space.
        Assert.True(MontoParser.TryParseMonto(" 1234,50 ", out var monto));
        Assert.Equal(1234.50m, monto);
    }

    [Fact]
    public void TryParseMonto_NegativeAmount_IsAcceptedOnPurpose()
    {
        // The padron import accepts negative credit on purpose, with a warning — not every
        // negative amount is invalid text.
        Assert.True(MontoParser.TryParseMonto("-1234", out var monto));
        Assert.Equal(-1234m, monto);
    }

    [Fact]
    public void TryParseMontoManual_ZeroDigitsToTheRightOfSeparator_IsUnambiguouslyDecimal()
    {
        // "1234," with nothing after the comma: 0 digits to the right, not 3, so it is decimal
        // (not the ambiguous case) in both modes.
        Assert.True(MontoParser.TryParseMontoManual("1234,", out var monto));
        Assert.Equal(1234m, monto);
    }
}
