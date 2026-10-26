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

            string ResolvePlaceholder(Match match)
            {
                // 名前が一致する値を探し、無ければ枠を残して開発者へ知らせる
                // Find the value with the matching name; otherwise keep the token and tell the developer
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
