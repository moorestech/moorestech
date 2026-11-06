// moorestech_client/Assets/Scripts/Client.Tests/Ui/Conventions/LookExceptionLedgerTest.cs
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// 台帳「見た目の例外」の ID とコードの look-exception の印の ID が過不足なく一致することを確かめる（L2・設計書 §3.4）
    /// Verifies the IDs in the "look exceptions" ledger and the look-exception markers in code match exactly (L2, design §3.4)
    /// </summary>
    public class LookExceptionLedgerTest
    {
        private const string LedgerPath = "docs/uitk/TODO.md";
        private const string SectionHeading = "## 見た目の例外";
        private const string ClientUiDirectory = "moorestech_client/Assets/Scripts/Client.Ui";
        private static readonly Regex LedgerId = new(@"^\|\s*(LX-\d+)\s*\|", RegexOptions.Multiline);
        private static readonly Regex Marker = new(@"look-exception:\s*(LX-\d+)");

        [Test]
        public void LedgerIdsAndCodeMarkersMatch()
        {
            var ledgerIds = LedgerId.Matches(ReadSection()).Select(match => match.Groups[1].Value).ToList();
            Assert.IsNotEmpty(ledgerIds, $"{SectionHeading} has no LX rows");
            Assert.AreEqual(ledgerIds.Count, ledgerIds.Distinct().Count(), "duplicate LX ids in the ledger");

            // 1 つの例外が複数の行に印を持ってよい
            // One exception may mark several lines
            var markerIds = new[] { "*.uss", "*.cs" }
                .SelectMany(pattern => Directory.GetFiles(Path.Combine(RepositoryRoot(), ClientUiDirectory), pattern, SearchOption.AllDirectories))
                .SelectMany(path => Marker.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
                .ToHashSet();
            CollectionAssert.AreEquivalent(ledgerIds.ToHashSet(), markerIds, "ledger ids and look-exception markers differ");
        }

        private static string ReadSection()
        {
            // 見出しから次の ## 見出しの前まで
            // From the heading up to the next ## heading
            var text = File.ReadAllText(Path.Combine(RepositoryRoot(), LedgerPath));
            var start = text.IndexOf(SectionHeading, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"{LedgerPath} has no {SectionHeading}");
            var end = text.IndexOf("\n## ", start + SectionHeading.Length, System.StringComparison.Ordinal);
            return end < 0 ? text.Substring(start) : text.Substring(start, end - start);
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
