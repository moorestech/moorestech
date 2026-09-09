using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.Gate
{
    /// <summary>
    /// uGUI再流入の決定論チェック。スクリーンスペースuGUI領域の全.csが分類済みで、除外以外がuGUIを参照しないことを機械判定する。
    /// Deterministic re-entry check: every screen-space uGUI .cs must be classified, and non-excluded files must not reference uGUI.
    /// </summary>
    public class WebUiGateAuditTest
    {
        // uGUI 参照の検出語。UIElements を部分一致で拾わないようセミコロンまで含める
        // uGUI reference tokens; the trailing semicolon keeps UIElements out of the match
        private static readonly string[] UguiTokens = { "using UnityEngine.UI;", "using TMPro;", "using UnityEngine.EventSystems;" };

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
                if (!Matches(relativePath, rule.PathPrefix)) continue;
                if (best == null || rule.PathPrefix.Length > best.Value.PathPrefix.Length) best = rule;
            }
            return best;
        }

        // ディレクトリ指定はパス区切りの境界で照合する（UI が UIToolkit を飲み込まないように）
        // Directory prefixes match on the path separator so "UI" never swallows "UIToolkit"
        private static bool Matches(string relativePath, string pathPrefix)
        {
            if (pathPrefix.EndsWith(".cs")) return relativePath == pathPrefix;
            return relativePath.StartsWith(pathPrefix + "/");
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

        // 分類の裏をかいて新規スクリーンスペースuGUIが入るのを止める
        // Stop new screen-space uGUI from slipping in behind the classification
        [Test]
        public void NonGatedFilesContainNoUguiToken()
        {
            var violations = new List<string>();
            foreach (var relativePath in EnumerateTargetFiles())
            {
                var rule = Resolve(relativePath);
                if (rule == null) continue;
                if (rule.Value.RuleCategory == WebUiGateClassification.Category.Excluded) continue;

                var text = File.ReadAllText(Path.Combine(ScriptsRoot, relativePath));
                foreach (var token in UguiTokens)
                {
                    if (text.Contains(token)) violations.Add($"{relativePath} ({token})");
                }
            }

            Assert.IsEmpty(violations,
                "除外に分類されていないファイルがスクリーンスペースuGUIを参照しています。Web UIへ寄せるか、分類をExcludedへ改めてください:\n" +
                string.Join("\n", violations));
        }

        // ルールの腐敗検出: 全ルールが実在ファイルに一致すること
        // Stale-rule detection: every rule must match at least one existing file
        [Test]
        public void AllRulesMatchExistingFiles()
        {
            var files = EnumerateTargetFiles().ToList();
            var stale = WebUiGateClassification.Rules
                .Where(rule => !files.Any(f => Matches(f, rule.PathPrefix)))
                .Select(rule => rule.PathPrefix)
                .ToList();
            Assert.IsEmpty(stale, "実在ファイルに一致しない分類ルール（削除・リネーム追従漏れ）:\n" + string.Join("\n", stale));
        }
    }
}
