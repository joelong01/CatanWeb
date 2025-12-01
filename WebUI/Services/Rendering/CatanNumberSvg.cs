using System.Text;

namespace Catan3.WebUI.Services.Rendering;

/// <summary>
/// Renders the Catan number token (circle with number and probability stars).
/// Returns a grouped SVG element so all parts can be positioned/styled together.
/// </summary>
public static class CatanNumberSvg
{
    /// <summary>
    /// Renders a CatanNumber token as an SVG group element.
    /// The group is centered at (0,0) - use transform="translate(x,y)" to position.
    /// </summary>
    /// <param name="number">The roll number (2-12).</param>
    /// <param name="radius">Circle radius. Default 30.</param>
    /// <returns>SVG group string containing circle and text elements.</returns>
    public static string Render(int number, double radius = 30)
    {
        var isHighProb = number == 6 || number == 8;
        var numberColor = isHighProb ? BoardSvgConstants.HighProbColor : BoardSvgConstants.NormalNumberColor;
        var pips = GetPips(number);

        // Scale font sizes proportionally to radius (base radius = 30)
        var scale = radius / 30.0;
        var numberFontSize = BoardSvgConstants.NumberFontSize * scale;
        var pipsFontSize = BoardSvgConstants.PipsFontSize * scale;
        var numberOffsetY = BoardSvgConstants.NumberOffsetY * scale;
        var pipsOffsetY = BoardSvgConstants.PipsOffsetY * scale;

        var sb = new StringBuilder();
        sb.AppendLine($@"<g class=""catan-number"">");

        // Background circle
        sb.AppendLine($@"  <circle cx=""0"" cy=""0"" r=""{radius}"" fill=""{BoardSvgConstants.NumberTokenFill}"" opacity=""{BoardSvgConstants.NumberTokenOpacity}"" stroke=""{BoardSvgConstants.NumberTokenStroke}"" stroke-width=""{BoardSvgConstants.NumberTokenStrokeWidth}""/>");

        // Roll number
        sb.AppendLine($@"  <text x=""0"" y=""{numberOffsetY}"" text-anchor=""middle"" dominant-baseline=""middle"" font-family=""sans-serif"" font-size=""{numberFontSize}"" font-weight=""bold"" fill=""{numberColor}"">{number}</text>");

        // Probability pips (stars)
        if (!string.IsNullOrEmpty(pips))
        {
            sb.AppendLine($@"  <text x=""0"" y=""{pipsOffsetY}"" text-anchor=""middle"" font-size=""{pipsFontSize}"" fill=""{numberColor}"">{pips}</text>");
        }

        sb.AppendLine("</g>");
        return sb.ToString();
    }

    /// <summary>
    /// Renders a CatanNumber token as a complete standalone SVG element.
    /// Useful for embedding in HTML outside of an existing SVG context.
    /// </summary>
    /// <param name="number">The roll number (2-12).</param>
    /// <param name="size">SVG width and height. Default 60.</param>
    /// <returns>Complete SVG element string.</returns>
    public static string RenderStandalone(int number, int size = 60)
    {
        var radius = size / 2.0;
        var center = size / 2.0;

        var sb = new StringBuilder();
        sb.AppendLine($@"<svg width=""{size}"" height=""{size}"" viewBox=""0 0 {size} {size}"">");
        sb.AppendLine($@"  <g transform=""translate({center},{center})"">");

        // Reuse the core rendering with appropriate radius
        var isHighProb = number == 6 || number == 8;
        var numberColor = isHighProb ? BoardSvgConstants.HighProbColor : BoardSvgConstants.NormalNumberColor;
        var pips = GetPips(number);

        // Scale font sizes proportionally (base radius = 30 for size = 60)
        var scale = radius / 30.0;
        var numberFontSize = BoardSvgConstants.NumberFontSize * scale;
        var pipsFontSize = BoardSvgConstants.PipsFontSize * scale;
        var numberOffsetY = BoardSvgConstants.NumberOffsetY * scale;
        var pipsOffsetY = BoardSvgConstants.PipsOffsetY * scale;

        // Background circle
        sb.AppendLine($@"    <circle cx=""0"" cy=""0"" r=""{radius - 1}"" fill=""{BoardSvgConstants.NumberTokenFill}"" opacity=""{BoardSvgConstants.NumberTokenOpacity}"" stroke=""black"" stroke-width=""2""/>");

        // Roll number
        sb.AppendLine($@"    <text x=""0"" y=""{numberOffsetY}"" text-anchor=""middle"" dominant-baseline=""middle"" font-family=""sans-serif"" font-size=""{numberFontSize}"" font-weight=""bold"" fill=""{numberColor}"">{number}</text>");

        // Probability pips (stars)
        if (!string.IsNullOrEmpty(pips))
        {
            sb.AppendLine($@"    <text x=""0"" y=""{pipsOffsetY}"" text-anchor=""middle"" font-size=""{pipsFontSize}"" fill=""{numberColor}"">{pips}</text>");
        }

        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// Gets the probability pips (stars) for a roll number.
    /// </summary>
    private static string GetPips(int number)
    {
        return number switch
        {
            2 or 12 => "★",
            3 or 11 => "★★",
            4 or 10 => "★★★",
            5 or 9 => "★★★★",
            6 or 8 => "★★★★★",
            _ => ""
        };
    }
}
