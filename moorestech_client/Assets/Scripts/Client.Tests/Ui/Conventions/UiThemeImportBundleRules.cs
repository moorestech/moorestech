// moorestech_client/Assets/Scripts/Client.Tests/Ui/Conventions/UiThemeImportBundleRules.cs
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// GameUiTheme.tss の束ね規約を検査し、違反を文で返す（L2・設計書 §1.4 C1・実行計画 E-1）
    /// Checks the GameUiTheme.tss bundling conventions and returns violations as sentences (L2, design C1, execution plan E-1)
    /// </summary>
    public static class UiThemeImportBundleRules
    {
        public const string UnityDefaultThemeUrl = "unity-theme://default";
        private const string ProjectUrlPrefix = "project://database/";
        private const string ClientUiAssetPrefix = "Assets/Scripts/Client.Ui/";

        // 族別区画の固定順。区画名は Client.Ui からの相対ディレクトリ
        // Fixed order of family sections; a section name is a directory relative to Client.Ui
        public static readonly string[] SectionOrder =
        {
            "Foundation/Theme", "Foundation/Stage",
            "Components/Panels", "Components/Slots", "Components/Controls", "Components/Gauges",
            "Components/Text", "Components/Decor", "Components/Recipe", "Components/NodeGraph",
            "Screens",
        };

        private static readonly string[] RootClassCheckedPrefixes = { ClientUiAssetPrefix + "Components/", ClientUiAssetPrefix + "Screens/" };
        private static readonly Regex ImportLine = new(@"^\s*@import\s+url\(""([^""]+)""\)\s*;\s*$");
        private static readonly Regex SectionLine = new(@"^\s*/\*\s*==\s*section:\s*([\w/]+)\s*==\s*\*/\s*$");
        private static readonly Regex Comment = new(@"/\*.*?\*/", RegexOptions.Singleline);
        private static readonly Regex FirstClassSelector = new(@"^\s*\.([\w-]+)", RegexOptions.Multiline);

        public static List<string> CheckBundle(string tssText, IReadOnlyCollection<string> clientUiUssAssetPaths)
        {
            var violations = new List<string>();
            var importCounts = new Dictionary<string, int>();
            string currentSection = null;
            var lastSectionIndex = -1;
            var importIndex = 0;
            var lines = tssText.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var section = SectionLine.Match(line);
                if (section.Success)
                {
                    CheckSection(section.Groups[1].Value, i + 1);
                    continue;
                }
                var import = ImportLine.Match(line);
                if (import.Success) CheckImport(import.Groups[1].Value, i + 1);
            }

            // Client.Ui の USS は全部ちょうど 1 回束ねる
            // Every Client.Ui USS is bundled exactly once
            foreach (var path in clientUiUssAssetPaths)
            {
                importCounts.TryGetValue(path, out var count);
                if (count != 1) violations.Add($"{path} is imported {count} times (expected 1)");
            }
            return violations;

            #region Internal

            void CheckSection(string name, int lineNumber)
            {
                var index = System.Array.IndexOf(SectionOrder, name);
                if (index < 0) violations.Add($"line {lineNumber}: unknown section '{name}'");
                else if (index <= lastSectionIndex) violations.Add($"line {lineNumber}: section '{name}' is out of order");
                lastSectionIndex = System.Math.Max(lastSectionIndex, index);
                currentSection = name;
            }

            void CheckImport(string url, int lineNumber)
            {
                importIndex++;
                // 先頭は Unity の既定テーマ。区画の外に置く
                // The first import is Unity's default theme, outside any section
                if (importIndex == 1)
                {
                    if (url != UnityDefaultThemeUrl) violations.Add($"line {lineNumber}: first import must be {UnityDefaultThemeUrl}");
                    return;
                }
                if (!url.StartsWith(ProjectUrlPrefix))
                {
                    violations.Add($"line {lineNumber}: '{url}' is not a project://database/ url");
                    return;
                }
                var assetPath = url.Substring(ProjectUrlPrefix.Length);
                importCounts[assetPath] = importCounts.TryGetValue(assetPath, out var count) ? count + 1 : 1;
                if (!clientUiUssAssetPaths.Contains(assetPath)) violations.Add($"line {lineNumber}: '{assetPath}' is not an existing Client.Ui USS");
                if (currentSection == null || !assetPath.StartsWith($"{ClientUiAssetPrefix}{currentSection}/")) violations.Add($"line {lineNumber}: '{assetPath}' is outside its section '{currentSection}'");
            }

            #endregion
        }

        public static List<string> CheckUniqueRootClasses(IReadOnlyDictionary<string, string> ussTextByAssetPath)
        {
            // 部品と画面の USS は最初の規則のクラスがルート。ルートは全 USS で一意
            // The first rule's class is the root of a component or screen USS; roots are unique across all USS
            var violations = new List<string>();
            var ownerByRoot = new Dictionary<string, string>();
            foreach (var (path, text) in ussTextByAssetPath.OrderBy(pair => pair.Key, System.StringComparer.Ordinal))
            {
                if (!RootClassCheckedPrefixes.Any(path.StartsWith)) continue;
                var match = FirstClassSelector.Match(Comment.Replace(text, ""));
                if (!match.Success)
                {
                    violations.Add($"{path} has no root class selector");
                    continue;
                }
                var root = match.Groups[1].Value;
                if (ownerByRoot.TryGetValue(root, out var owner)) violations.Add($"root class .{root} is shared by {owner} and {path}");
                else ownerByRoot.Add(root, path);
            }
            return violations;
        }
    }
}
