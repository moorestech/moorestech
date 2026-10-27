using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Client.Localization
{
    // 辞書テンプレの{name}を名前付きの値で埋める
    // Fills a dictionary template's {name} placeholders with named values
    internal static class LocalizationTextInterpolator
    {
        private static readonly Regex PlaceholderPattern = new(@"\{([^{}]+)\}");

        internal static string Interpolate(string keyForLog, string template, IReadOnlyList<LocalizationTextParam> textParams)
        {
            ReportDuplicateParamNames();
            var builder = new StringBuilder(template.Length);
            var cursor = 0;
            foreach (Match match in PlaceholderPattern.Matches(template))
            {
                builder.Append(template, cursor, match.Index - cursor);
                builder.Append(ResolvePlaceholder(match));
                cursor = match.Index + match.Length;
            }
            builder.Append(template, cursor, template.Length - cursor);
            return builder.ToString();

            #region Internal

            // 同名は前から探す ResolvePlaceholder で先勝ちになる。重複は黙らずエラーログで知らせる（TooltipTopic と同じ規則）
            // ResolvePlaceholder scans from the front, so a duplicate name resolves first-wins; it is logged as an error (same rule as TooltipTopic)
            void ReportDuplicateParamNames()
            {
                for (var later = 1; later < textParams.Count; later++)
                {
                    for (var earlier = 0; earlier < later; earlier++)
                    {
                        if (textParams[earlier].Name != textParams[later].Name) continue;
                        Debug.LogError($"[Localize] {keyForLog}: duplicate param '{textParams[later].Name}'; the first value wins");
                        break;
                    }
                }
            }

            string ResolvePlaceholder(Match match)
            {
                // 一致値を探し、無ければ枠残しで通知
                // Find the matching value; keep the slot and report if missing
                var name = match.Groups[1].Value;
                foreach (var textParam in textParams)
                {
                    if (textParam.Name == name) return textParam.Value;
                }
                Debug.LogError($"[Localize] {keyForLog}: no value for placeholder {match.Value}");
                return match.Value;
            }

            #endregion
        }
    }
}
