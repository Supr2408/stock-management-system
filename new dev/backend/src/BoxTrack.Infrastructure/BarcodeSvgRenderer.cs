using System.Net;
using System.Text;

namespace BoxTrack.Infrastructure;

internal static class BarcodeSvgRenderer
{
    private static readonly string[] Code128Patterns =
    [
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112"
    ];

    public static string RenderLabel(string barcode, string itemName, string description, string logoMode, string? logoDataUri)
    {
        var header = logoMode switch
        {
            "WithoutLogo" => string.Empty,
            "WithALUFO" => "<text x=\"146\" y=\"38\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"24\" font-weight=\"700\">ALUFO</text><text x=\"146\" y=\"55\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"9\">NAGREEKA INDCON PRODUCTS (P) LTD</text>",
            "Custom" when !string.IsNullOrWhiteSpace(logoDataUri) => $"<image href=\"{WebUtility.HtmlEncode(logoDataUri)}\" x=\"66\" y=\"12\" width=\"160\" height=\"42\" preserveAspectRatio=\"xMidYMid meet\"/>",
            _ => "<text x=\"146\" y=\"36\" text-anchor=\"middle\" font-family=\"Georgia,serif\" font-size=\"25\" font-weight=\"700\">Nagreeka</text><text x=\"146\" y=\"55\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"9\">NAGREEKA INDCON PRODUCTS (P) LTD</text>"
        };
        var bars = RenderCode128Bars(barcode, 32, 88, 228, 72);
        return $"""
<svg xmlns="http://www.w3.org/2000/svg" width="292" height="184" viewBox="0 0 292 184">
  <rect width="292" height="184" fill="#fff"/>
  {header}
  <text x="146" y="75" text-anchor="middle" font-family="Arial" font-size="14" font-weight="700">{Xml(itemName)}</text>
  {bars}
  <text x="146" y="174" text-anchor="middle" font-family="Consolas,monospace" font-size="15" font-weight="700">{Xml(barcode)}</text>
  <text x="146" y="24" text-anchor="middle" font-family="Arial" font-size="7" fill="#777">{Xml(description)}</text>
</svg>
""";
    }

    private static string RenderCode128Bars(string value, int x, int y, int width, int height)
    {
        var encoded = EncodeCode128B(value);
        var modules = encoded.Sum(code => Code128Patterns[code].Sum(character => character - '0'));
        var moduleWidth = width / (decimal)modules;
        var cursor = (decimal)x;
        var builder = new StringBuilder();
        foreach (var code in encoded)
        {
            var pattern = Code128Patterns[code];
            for (var index = 0; index < pattern.Length; index++)
            {
                var barWidth = (pattern[index] - '0') * moduleWidth;
                if (index % 2 == 0)
                    builder.Append(FormattableString.Invariant($"<rect x=\"{cursor:0.###}\" y=\"{y}\" width=\"{barWidth:0.###}\" height=\"{height}\" fill=\"#111\"/>"));
                cursor += barWidth;
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<int> EncodeCode128B(string value)
    {
        const int startB = 104;
        const int stop = 106;
        var codes = new List<int> { startB };
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is < ' ' or > '~') throw new InvalidOperationException("Barcode text contains unsupported characters.");
            codes.Add(character - 32);
        }

        var checksum = startB;
        for (var index = 1; index < codes.Count; index++) checksum += codes[index] * index;
        codes.Add(checksum % 103);
        codes.Add(stop);
        return codes;
    }

    private static string Xml(string value) => WebUtility.HtmlEncode(value);
}
