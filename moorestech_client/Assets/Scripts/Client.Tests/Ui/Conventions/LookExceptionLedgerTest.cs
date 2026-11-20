// moorestech_client/Assets/Scripts/Client.Tests/Ui/Conventions/LookExceptionLedgerTest.cs
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// 台帳IDと印のIDの一致を確かめる（L2）
    /// Verifies ledger IDs match marker IDs (L2)
    /// </summary>
    public class LookExceptionLedgerTest
    {
        private const string LedgerPath = "docs/uitk/look-exceptions.md";
        private const string ClientUiDirectory = "moorestech_client/Assets/Scripts/Client.Ui";
        private static readonly Regex LedgerId = new(@"^\|\s*(LX-\d+)\s*\|", RegexOptions.Multiline);
        private static readonly Regex Marker = new(@"look-exception:\s*(LX-\d+)");

        [Test]
        public void LedgerIdsAndCodeMarkersMatch()
        {
            var ledgerText = File.ReadAllText(Path.Combine(RepositoryRoot(), LedgerPath));
            var ledgerIds = LedgerId.Matches(ledgerText).Select(match => match.Groups[1].Value).ToList();
            Assert.IsNotEmpty(ledgerIds, $"{LedgerPath} has no LX rows");
            Assert.AreEqual(ledgerIds.Count, ledgerIds.Distinct().Count(), "duplicate LX ids in the ledger");

            // 1 つの例外が複数の行に印を持ってよい
            // One exception may mark several lines
            var markerIds = new[] { "*.uss", "*.cs" }
                .SelectMany(pattern => Directory.GetFiles(Path.Combine(RepositoryRoot(), ClientUiDirectory), pattern, SearchOption.AllDirectories))
                .SelectMany(path => Marker.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
                .ToHashSet();
            CollectionAssert.AreEquivalent(ledgerIds.ToHashSet(), markerIds, "ledger ids and look-exception markers differ");
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
