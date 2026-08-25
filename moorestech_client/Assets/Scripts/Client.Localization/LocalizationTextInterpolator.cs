using System.Collections.Generic;

namespace Client.Localization
{
    // 辞書テンプレートの{p0}プレースホルダを埋める（Web側translatorと同じ規約）
    // Fills the {p0} placeholders of a dictionary template, matching the web translator convention
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
