using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// Web のトークン CSS と UITK のトークン USS＋除外表が名前で過不足なく対応することを確かめる（L2）
    /// Verifies the Web token CSS maps one-to-one by name onto the UITK token USS plus the exclusion table (L2)
    /// </summary>
    public class WebTokenParityTest
    {
        private const string WebTokenDirectory = "moorestech_web/webui/src/app";
        private const string UssTokenDirectory = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme";
        private static readonly string[] WebTokenFiles = { "tokens.css", "skitTokens.css", "slotTokens.css" };
        private static readonly string[] UssTokenFiles = { "Tokens.uss", "SkitTokens.uss", "SlotTokens.uss" };
        private const string ExclusionFile = "TokenConversionExclusions.txt";

        private static readonly Regex CommentPattern = new(@"/\*[\s\S]*?\*/");
        private static readonly Regex DeclaredNamePattern = new(@"(--[\w-]+)\s*:");

        [Test]
        public void CssTokenNamesEqualUssTokenNamesPlusExclusions()
        {
            var cssNames = ReadDeclaredNames(WebTokenDirectory, WebTokenFiles);
            var ussNames = ReadDeclaredNames(UssTokenDirectory, UssTokenFiles);
            var excludedNames = ReadExcludedNames();

            // USS と除外表は交わらず、合わせると CSS と同じ集合になる
            // USS and the exclusion table are disjoint and together equal the CSS set
            var overlap = ussNames.Intersect(excludedNames).ToList();
            Assert.IsEmpty(overlap, $"Both converted and excluded: {string.Join(", ", overlap)}");

            var covered = new HashSet<string>(ussNames.Concat(excludedNames));
            var missing = cssNames.Except(covered).ToList();
            var extra = covered.Except(cssNames).ToList();
            Assert.IsEmpty(missing, $"CSS tokens missing from USS and exclusions (regenerate with node tools/uitk/convert-web-tokens.mjs): {string.Join(", ", missing)}");
            Assert.IsEmpty(extra, $"USS/exclusion names absent from CSS: {string.Join(", ", extra)}");
        }

        [Test]
        public void UssTokensContainNoUnconvertedCssSyntax()
        {
            // USS が解釈しない CSS 構文が変換後に残っていない
            // No CSS syntax that USS cannot parse remains after conversion
            var forbidden = new[] { "calc(", "rem", "color-mix", "rgb(from" };
            foreach (var fileName in UssTokenFiles)
            {
                var text = File.ReadAllText(Path.Combine(RepositoryRoot(), UssTokenDirectory, fileName));
                foreach (var syntax in forbidden)
                {
                    Assert.IsFalse(text.Contains(syntax), $"{fileName} still contains '{syntax}'");
                }
            }
        }

        private static HashSet<string> ReadDeclaredNames(string directory, IEnumerable<string> fileNames)
        {
            var names = new HashSet<string>();
            foreach (var fileName in fileNames)
            {
                // コメントを除いてから「--name:」の宣言だけを拾う（var(--x) の参照は拾わない）
                // Strip comments, then pick only "--name:" declarations (not var(--x) references)
                var text = CommentPattern.Replace(File.ReadAllText(Path.Combine(RepositoryRoot(), directory, fileName)), "");
                foreach (Match match in DeclaredNamePattern.Matches(text)) names.Add(match.Groups[1].Value);
            }
            return names;
        }

        private static HashSet<string> ReadExcludedNames()
        {
            var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), UssTokenDirectory, ExclusionFile));
            var names = new HashSet<string>();
            foreach (var line in lines.Where(line => line.Length > 0))
            {
                // 1 行は「--name<TAB>理由」。理由の無い行は表の破損
                // Each line is "--name<TAB>reason"; a line without a reason means the table is broken
                var columns = line.Split('\t');
                Assert.AreEqual(2, columns.Length, $"Malformed exclusion line: {line}");
                Assert.IsNotEmpty(columns[1].Trim(), $"Exclusion without a reason: {line}");
                Assert.IsTrue(names.Add(columns[0]), $"Duplicate exclusion: {columns[0]}");
            }
            return names;
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
