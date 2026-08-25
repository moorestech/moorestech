using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Localization.Resolution
{
    /// <summary>
    /// 14箇所のローカライズ置換を守る決定論チェック。対象ファイルへ日本語リテラルが再露出しないことを機械判定する。
    /// Deterministic guard for the 14 localized call sites: fails if a Japanese literal ever creeps back in.
    /// </summary>
    public class ProductionCallsiteLocalizationGuardTest
    {
        private static readonly string[] TargetRelativePaths =
        {
            "Client.Starter/Initialization/ModAssetLoader.cs",
            "Client.Starter/Initialization/ModAssetIconLoader.cs",
            "Client.Starter/Initialization/ServerConnectionInitializer.cs",
            "Client.Starter/InitializeScenePipeline.cs",
            "Client.MainMenu/ConnectServer.cs",
        };

        // ひらがな・カタカナ・CJK統合漢字を検出（コメント・Debug.LogError行は対象外）
        // Matches hiragana/katakana/CJK ideographs (comment and Debug.LogError lines are excluded)
        private static readonly Regex JapaneseCharacters = new(@"[぀-ヿ一-鿿]");

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "Scripts");

        [Test]
        public void TargetFilesContainNoJapaneseStringLiterals()
        {
            var violations = new List<string>();
            foreach (var relativePath in TargetRelativePaths)
            {
                var absolutePath = Path.Combine(ScriptsRoot, relativePath);
                var lineNumber = 0;
                foreach (var line in File.ReadLines(absolutePath))
                {
                    lineNumber++;
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (trimmed.Contains("Debug.LogError")) continue;
                    if (JapaneseCharacters.IsMatch(line))
                    {
                        violations.Add($"{relativePath}:{lineNumber}: {line.Trim()}");
                    }
                }
            }

            Assert.IsEmpty(violations,
                "日本語リテラルが再露出しています。Localize.Get/GetFormattedへ戻してください:\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ConnectServerPortValidationBranchesMapToExpectedKeys()
        {
            var text = File.ReadAllText(Path.Combine(ScriptsRoot, "Client.MainMenu/ConnectServer.cs"));

            AssertBranchMapsToKey(text, "MaxPort < port", "ConnectPortTooLarge");
            AssertBranchMapsToKey(text, "port <= MinExclusivePort", "ConnectPortTooSmall");

            #region Internal

            void AssertBranchMapsToKey(string source, string condition, string expectedKey)
            {
                var conditionIndex = source.IndexOf(condition, StringComparison.Ordinal);
                Assert.GreaterOrEqual(conditionIndex, 0, $"条件 '{condition}' が見つかりません");

                var returnIndex = source.IndexOf("return;", conditionIndex, StringComparison.Ordinal);
                Assert.GreaterOrEqual(returnIndex, 0, $"'{condition}' の後にreturn;が見つかりません");

                var branchBody = source.Substring(conditionIndex, returnIndex - conditionIndex);
                StringAssert.Contains(expectedKey, branchBody, $"'{condition}' の分岐に {expectedKey} が対応していません");
            }

            #endregion
        }
    }
}
