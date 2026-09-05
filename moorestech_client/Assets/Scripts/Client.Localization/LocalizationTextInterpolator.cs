using System.Collections.Generic;

namespace Client.Localization
{
    // 辞書テンプレの{p0}を埋める
    // Fills a dictionary template's {p0} placeholders
    internal static class LocalizationTextInterpolator
    {
        public static string Interpolate(string template, IReadOnlyList<string> textParams)
        {
            var text = template;
            for (var index = 0; index < textParams.Count; index++)
            {
                text = text.Replace($"{{p{index}}}", textParams[index]);
            }

            return text;
        }
    }
}
