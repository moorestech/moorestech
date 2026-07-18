using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.Gate
{
    /// <summary>
    /// ゲート漏れ決定論チェック。スクリーンスペースuGUI領域の全.csが分類済みで、ゲートルートが実際にゲートを持つことを機械判定する。
    /// Deterministic gate-leak check: every screen-space uGUI .cs must be classified, and gated roots must actually contain the gate.
    /// </summary>
    public class WebUiGateAuditTest
    {
        private const string GateToken = "WebUiScreenGate.IsWebUiMode";

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "Scripts");

        // 走査対象の全.csを列挙する（.metaは対象外）
        // Enumerate all .cs files under the scan roots (excluding .meta)
        private static IEnumerable<string> EnumerateTargetFiles()
        {
            foreach (var root in WebUiGateClassification.ScanRoots)
            {
                var abs = Path.Combine(ScriptsRoot, root);
                if (!Directory.Exists(abs)) continue;
                foreach (var file in Directory.EnumerateFiles(abs, "*.cs", SearchOption.AllDirectories))
                {
                    yield return ToRelative(file);
                }
            }
        }

        private static string ToRelative(string absolutePath)
        {
            return absolutePath.Replace('\\', '/').Substring(ScriptsRoot.Replace('\\', '/').Length + 1);
        }

        // 最長一致ルールを解決する。未分類ならnull
        // Resolve the longest-prefix rule; null when unclassified
        private static WebUiGateClassification.Rule? Resolve(string relativePath)
        {
            WebUiGateClassification.Rule? best = null;
            foreach (var rule in WebUiGateClassification.Rules)
            {
                if (!relativePath.StartsWith(rule.PathPrefix)) continue;
                if (best == null || rule.PathPrefix.Length > best.Value.PathPrefix.Length) best = rule;
            }
            return best;
        }

        // 新規スクリーンスペースuGUIの未分類追加を禁止する
        // Forbid adding unclassified screen-space uGUI files
        [Test]
        public void AllScreenSpaceUiFilesAreClassified()
        {
            var unclassified = EnumerateTargetFiles().Where(f => Resolve(f) == null).ToList();
            Assert.IsEmpty(unclassified,
                "未分類のスクリーンスペースuGUIファイルがあります。WebUiGateClassification.Rules へ処遇（ゲート/Phase/除外）を追加してください:\n" +
                string.Join("\n", unclassified));
        }

        // ゲートルートは実際に WebUiScreenGate.IsWebUiMode を参照していること
        // Every gated root must actually reference WebUiScreenGate.IsWebUiMode
        [Test]
        public void GatedRootsContainGateToken()
        {
            var missing = new List<string>();
            foreach (var rule in WebUiGateClassification.Rules)
            {
                if (rule.RuleCategory != WebUiGateClassification.Category.GatedRoot) continue;
                var abs = Path.Combine(ScriptsRoot, rule.PathPrefix);
                if (!File.Exists(abs))
                {
                    missing.Add($"{rule.PathPrefix} (ファイルが存在しない — リネーム時はルールも更新)");
                    continue;
                }
                if (!File.ReadAllText(abs).Contains(GateToken)) missing.Add($"{rule.PathPrefix} (ゲート参照が消えている)");
            }
            Assert.IsEmpty(missing, "ゲートルートの検証に失敗:\n" + string.Join("\n", missing));
        }

        // ルールの腐敗検出: 全ルールが実在ファイルに一致すること
        // Stale-rule detection: every rule must match at least one existing file
        [Test]
        public void AllRulesMatchExistingFiles()
        {
            var files = EnumerateTargetFiles().ToList();
            var stale = WebUiGateClassification.Rules
                .Where(rule => !files.Any(f => f.StartsWith(rule.PathPrefix)))
                .Select(rule => rule.PathPrefix)
                .ToList();
            Assert.IsEmpty(stale, "実在ファイルに一致しない分類ルール（削除・リネーム追従漏れ）:\n" + string.Join("\n", stale));
        }
    }
}
