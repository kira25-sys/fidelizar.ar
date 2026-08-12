using System.Globalization;

namespace Fidelizar.Domain.Money;

/// <summary>
/// The only place in the repo that parses money out of text. It has two modes because there are
/// two genuinely different situations and one rule cannot serve both:
///
/// <list type="bullet">
/// <item>
/// <b>Export mode</b> (<see cref="TryParseMonto(string?, out decimal, out bool)"/>): used by the
/// padron importer (the roster spreadsheet's initial credit — F0-08) and, from phase 2 on, by the
/// canonical sales reader (a sale's total). There the text was produced by some system under
/// whatever regional settings it happened to have, and we do not know which — so "1234,50",
/// "1.234,50" and "1,234.50" all have to resolve to the same value, and some texts genuinely
/// cannot be guessed without risking a member's money.
/// </item>
/// <item>
/// <b>Hand-typed mode</b> (<see cref="TryParseMontoManual"/>): for an amount a person typed by
/// hand — a counter screen, an admin form. It shares the main rule with export mode (the number of
/// digits to the right decides whether the separator is decimal); it differs only in the single
/// genuinely ambiguous case — exactly 3 digits to the right — where, because we know a person
/// typed it, the tie is broken with the River Plate convention (thousands dot, decimal comma)
/// instead of being rejected.
/// </item>
/// </list>
/// </summary>
public static class MontoParser
{
    private enum AmountParseMode
    {
        /// <summary>We do not know who exported the text: a single separator used exactly once
        /// with exactly 3 digits to the right is rejected as ambiguous instead of guessed.</summary>
        Export,

        /// <summary>We know a person typed it following the River Plate convention: like
        /// <see cref="Export"/>, a single separator used once with a digit count to the right
        /// other than 3 is unambiguously decimal; the exactly-3-digits case, instead of being
        /// rejected, is broken by the separator's identity (dot = thousands, comma = decimal).</summary>
        Manual,
    }

    /// <summary>
    /// Tries to parse a money amount <b>exported by a system</b>, tolerating a decimal separator
    /// of comma or dot and a thousands separator of the other one. For text typed by an identified
    /// person, use <see cref="TryParseMontoManual"/> instead: there the ambiguity resolves
    /// differently because we do know who wrote it.
    ///
    /// <para>
    /// <b>Never uses <see cref="NumberStyles.Any"/>.</b> That combination lets
    /// <c>decimal.TryParse</c> accept any number of digits between thousands separators (.NET does
    /// not validate group sizes), so "1234,50" used to be read with the comma as a thousands
    /// separator and produced 123450 instead of 1234.50 — a hundred times too large. Here the text
    /// is normalised by hand under the rules below and only then parsed with
    /// <see cref="CultureInfo.InvariantCulture"/> and a narrow <see cref="NumberStyles"/>.
    /// </para>
    ///
    /// <para><b>Rules, in this order:</b></para>
    /// <list type="number">
    /// <item>
    /// Strip '$', whitespace (including the hard space U+00A0), and tolerate a leading minus sign
    /// — the padron import accepts negative credit on purpose, with a warning.
    /// </item>
    /// <item>
    /// If both dot and comma appear in the same text: whichever is further right is the decimal
    /// separator, the other is thousands. Unambiguous ("1.234,50" and "1,234.50" both give
    /// 1234.50).
    /// </item>
    /// <item>If only one kind of separator appears, repeated ("1.234.567"): it is thousands.</item>
    /// <item>
    /// If a single separator appears once, look at how many digits are left to its right:
    /// anything other than 3 ("1234,50", "1234,5", "1234.5678") is the decimal separator. Exactly
    /// 3 ("1,234", "1.234") is <b>genuinely ambiguous</b>: it could be one thousand two hundred
    /// thirty-four (thousands separator, the usual export shape) or a decimal comma followed by
    /// three digits that are really hundredths with an extra zero — there is no way to know
    /// without asking. Guessing here is gambling with a member's money, so this returns
    /// <c>false</c> (with <paramref name="ambiguous"/> set to true) and the caller reports it as
    /// an unreadable row so it gets re-exported with a decimal dot.
    /// </item>
    /// <item>No separators: a plain integer.</item>
    /// </list>
    /// </summary>
    /// <param name="text">The text to parse, exactly as it comes from the cell or column.</param>
    /// <param name="monto">The parsed amount, or 0 if it could not be parsed.</param>
    /// <param name="ambiguous">
    /// True when the <c>false</c> return is due to rule 4's ambiguity (a single separator, used
    /// once, with exactly 3 digits to the right) rather than plain invalid text. Lets the caller
    /// word a different message ("the number is ambiguous, re-export with a decimal dot") than a
    /// generic one ("not a number").
    /// </param>
    public static bool TryParseMonto(string? text, out decimal monto, out bool ambiguous) =>
        TryParseMontoInternal(text, AmountParseMode.Export, out monto, out ambiguous);

    /// <summary>Overload for when the caller does not care to distinguish the reason for <c>false</c>.</summary>
    public static bool TryParseMonto(string? text, out decimal monto) => TryParseMonto(text, out monto, out _);

    /// <summary>
    /// Tries to parse a money amount <b>typed by hand by a person</b> (e.g. an amount entered on a
    /// counter screen or an admin form).
    ///
    /// <para>
    /// Shares with <see cref="TryParseMonto(string?, out decimal, out bool)"/> (export mode) its
    /// main rule: with a single separator used once, the number of digits to the right decides
    /// whether it is decimal ("1234.50" → 1234.50, "1234,50" → 1234.50, "1.2345" → 1.2345,
    /// regardless of whether the separator is a dot or a comma). This is deliberate: it is what
    /// the original <c>NumberStyles.Any</c> got right, and breaking it would just move the 100x
    /// bug elsewhere instead of fixing it — plenty of people type a dot as a decimal separator,
    /// especially from a numeric keypad or copying a value off a screen.
    /// </para>
    ///
    /// <para>
    /// The difference from export mode is <b>only</b> the exactly-3-digits-to-the-right case, the
    /// one genuinely ambiguous one: here, instead of rejecting it, the separator's identity breaks
    /// the tie using the River Plate convention — because here we do know a person typed it by
    /// hand. Dot = thousands separator ("1.500" → 1500), comma = decimal separator ("1,500" → 1.5).
    /// </para>
    ///
    /// <para>
    /// Examples: "1234.50" → 1234.50, "1234,50" → 1234.50, "1.234,50" → 1234.50, "1.500" → 1500,
    /// "1,500" → 1.5.
    /// </para>
    /// </summary>
    /// <param name="text">The text exactly as the person typed it.</param>
    /// <param name="monto">The parsed amount, or 0 if it could not be parsed.</param>
    public static bool TryParseMontoManual(string? text, out decimal monto) =>
        TryParseMontoInternal(text, AmountParseMode.Manual, out monto, out _);

    private static bool TryParseMontoInternal(string? text, AmountParseMode mode, out decimal monto, out bool ambiguous)
    {
        monto = 0m;
        ambiguous = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = new string(text.Where(c => c != '$' && !char.IsWhiteSpace(c)).ToArray());

        var isNegative = cleaned.StartsWith('-');
        if (isNegative)
        {
            cleaned = cleaned[1..];
        }

        // After stripping the sign and money separators, only digits, dots and commas may remain:
        // any other letter (or an empty text after the sign) is garbage, not an amount.
        if (cleaned.Length == 0 || !cleaned.All(c => char.IsAsciiDigit(c) || c is '.' or ','))
        {
            return false;
        }

        var dotIndex = cleaned.LastIndexOf('.');
        var commaIndex = cleaned.LastIndexOf(',');
        string normalized;

        if (dotIndex >= 0 && commaIndex >= 0)
        {
            // Both kinds appear: whichever is further right is the decimal one. Unambiguous, and
            // this rule is the same in both modes.
            normalized = commaIndex > dotIndex
                ? cleaned.Replace(".", string.Empty).Replace(',', '.')
                : cleaned.Replace(",", string.Empty);
        }
        else if (dotIndex >= 0 || commaIndex >= 0)
        {
            var separator = dotIndex >= 0 ? '.' : ',';

            if (cleaned.Count(c => c == separator) > 1)
            {
                // The same separator appears more than once: it can only be thousands
                // ("1.234.567"), a decimal never repeats. Same rule in both modes.
                normalized = cleaned.Replace(separator.ToString(), string.Empty);
            }
            else
            {
                // A single separator, used once: the number of digits to the right decides in
                // both modes ("1234.50", "1234,50" and "1.2345" are unambiguously decimal,
                // whatever the separator — this is what the old NumberStyles.Any got right and
                // must not be broken). Exactly 3 digits to the right is the only genuinely
                // ambiguous case, and that is where the two modes diverge.
                var separatorIndex = separator == '.' ? dotIndex : commaIndex;
                var digitsToRight = cleaned.Length - separatorIndex - 1;

                if (digitsToRight != 3)
                {
                    normalized = separator == '.' ? cleaned : cleaned.Replace(',', '.');
                }
                else if (mode == AmountParseMode.Manual)
                {
                    // River Plate convention breaks the tie: dot = thousands, comma = decimal.
                    normalized = separator == '.'
                        ? cleaned.Replace(".", string.Empty)
                        : cleaned.Replace(',', '.');
                }
                else
                {
                    ambiguous = true;
                    return false;
                }
            }
        }
        else
        {
            // No separators: a plain integer.
            normalized = cleaned;
        }

        if (isNegative)
        {
            normalized = "-" + normalized;
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out monto);
    }
}
