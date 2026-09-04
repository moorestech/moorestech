using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Localization.Resolution
{
    /// <summary>
    /// 対象ファイルへの日本語リテラル再露出を機械判定
    /// Mechanically detects Japanese literals creeping back into the target files
    /// </summary>
    public class ProductionCallsiteLocalizationGuardTest
    {
        private static readonly string[] TargetRelativePaths =
        {
            "Client.Starter/Initialization/ModAssetLoader.cs",
            "Client.Starter/Initialization/ModAssetIconLoader.cs",
            "Client.Starter/Initialization/ServerConnectionInitializer.cs",
            "Client.Starter/Initialization/Progress/LoadingProgressLog.cs",
            "Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs",
            "Client.Starter/InitializeScenePipeline.cs",
            "Client.MainMenu/ConnectServer.cs",
        };

        // かな・CJKを検出（コメントとDebug.LogError行は除外）
        // Matches kana/CJK, excluding comment and Debug.LogError lines
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
    }
}
