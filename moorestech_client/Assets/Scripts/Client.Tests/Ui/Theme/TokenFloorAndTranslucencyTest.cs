using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 文字寸法の下限（13px）と半透明トークンの表が USS と食い違わないことを確かめる（L2・S7）
    /// Verifies the 13px text-size floor and that the translucent-token table matches the USS (L2, S7)
    /// </summary>
    public class TokenFloorAndTranslucencyTest
    {
        private const string UiScriptDirectory = "moorestech_client/Assets/Scripts/Client.Ui";
        private const string TranslucentTablePath = "docs/uitk/translucent-tokens.md";
        private const float MinimumFontSizePx = 13f;
        private static readonly HashSet<string> ExemptTokenNames = new() { "--fluid-amount-font-size", "--inventory-count-base-font-size" };
        private static readonly Regex FontSizeTokenPattern = new(@"(--[\w-]+-(?:font|text)-size)\s*:\s*([\d.]+)px");
        private static readonly Regex FontSizePropertyPattern = new(@"(?<![\w-])font-size\s*:\s*([\d.]+)px");
        private static readonly Regex TranslucentRgbaPattern = new(@"(--[\w-]+)\s*:\s*rgba\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*(0?\.\d+)\s*\)");

        [Test]
        public void NoUssTextSizeBelowThirteenPixels()
        {
            foreach (var path in UssFiles())
            {
                var text = File.ReadAllText(path);
                foreach (Match match in FontSizeTokenPattern.Matches(text))
                {
                    if (ExemptTokenNames.Contains(match.Groups[1].Value)) continue;
                    Assert.GreaterOrEqual(ParsePx(match.Groups[2].Value), MinimumFontSizePx, $"{Path.GetFileName(path)}: {match.Value}");
                }
                foreach (Match match in FontSizePropertyPattern.Matches(text))
                {
                    Assert.GreaterOrEqual(ParsePx(match.Groups[1].Value), MinimumFontSizePx, $"{Path.GetFileName(path)}: {match.Value}");
                }
            }
        }

        [Test]
        public void TranslucentTableListsEveryTranslucentToken()
        {
            // USS の半透明トークンと表の行が名前で一致する
            // The USS translucent tokens and the table rows match by name
            var ussNames = UssFiles().SelectMany(path => TranslucentRgbaPattern.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value)).ToHashSet();
            var tableText = File.ReadAllText(Path.Combine(RepositoryRoot(), TranslucentTablePath));
            var tableNames = Regex.Matches(tableText, @"^\| `(--[\w-]+)` \|", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToHashSet();
            CollectionAssert.AreEquivalent(ussNames, tableNames);
        }

        private static IEnumerable<string> UssFiles()
        {
            return Directory.GetFiles(Path.Combine(RepositoryRoot(), UiScriptDirectory), "*.uss", SearchOption.AllDirectories);
        }

        private static float ParsePx(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
