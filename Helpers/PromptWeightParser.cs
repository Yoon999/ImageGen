using System.Globalization;
using System.Text.RegularExpressions;

namespace ImageGen.Helpers;

public sealed record PromptWeightSpan(int Start, int Length, double Weight, string Content);

public static class PromptWeightParser
{
    private static readonly Regex WeightRegex = new(
        @"(-?\d+(?:\.\d+)?)::(.+?)::",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex ExactWeightRegex = new(
        @"^(-?\d+(?:\.\d+)?)::(.+)::$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public static IReadOnlyList<PromptWeightSpan> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<PromptWeightSpan>();

        var spans = new List<PromptWeightSpan>();
        foreach (Match match in WeightRegex.Matches(text))
        {
            if (!TryParseWeight(match.Groups[1].Value, out double weight)) continue;

            spans.Add(new PromptWeightSpan(
                match.Index,
                match.Length,
                weight,
                match.Groups[2].Value));
        }

        return spans;
    }

    public static bool TryParseExact(string text, out PromptWeightSpan? span)
    {
        Match match = ExactWeightRegex.Match(text);
        if (!match.Success || !TryParseWeight(match.Groups[1].Value, out double weight))
        {
            span = null;
            return false;
        }

        span = new PromptWeightSpan(0, match.Length, weight, match.Groups[2].Value);
        return true;
    }

    private static bool TryParseWeight(string value, out double weight)
    {
        return double.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out weight)
            && double.IsFinite(weight);
    }
}
