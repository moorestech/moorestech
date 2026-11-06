using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 半透明表がUSSと一致（L2）
    /// Translucent table matches USS (L2)
    /// </summary>
    public class TokenFloorAndTranslucencyTest
    {
        private const string UiScriptDirectory = "moorestech_client/Assets/Scripts/Client.Ui";
        private const string TranslucentTablePath = "docs/uitk/translucent-tokens.md";
        private static readonly Regex TranslucentRgbaPattern = new(@"(--[\w-]+)\s*:\s*rgba\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*(0?\.\d+)\s*\)");

        [Test]
        public void GeneratedTokensCarryNoFontFloor()
        {
            // 文字寸法はWebと同じpx
            // Text sizes match the Web px
            foreach (var path in UssFiles()) StringAssert.DoesNotContain("下限", File.ReadAllText(path), Path.GetFileName(path));
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

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
