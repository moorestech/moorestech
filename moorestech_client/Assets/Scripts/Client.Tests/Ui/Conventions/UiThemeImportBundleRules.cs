// moorestech_client/Assets/Scripts/Client.Tests/Ui/Conventions/UiThemeImportBundleRules.cs
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// tssの束ね規約を検査（L2）
    /// Checks the tss bundling rules (L2)
    /// </summary>
    public static class UiThemeImportBundleRules
    {
        private const string UnityDefaultThemeUrl = "unity-theme://default";
        private const string ProjectUrlPrefix = "project://database/";
        private const string ClientUiAssetPrefix = "Assets/Scripts/Client.Ui/";

        // 区画の固定順
        // Fixed section order
        public static readonly string[] SectionOrder =
        {
            "Foundation/Theme", "Foundation/Stage", "Foundation/Filter", "Foundation/Tooltip", "Foundation/Modal",
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

            // USSは各1回束ねる
            // Each USS is bundled once
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
                // 先頭は既定テーマ
                // The first import is the default theme
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
            // ルートクラスは全USSで一意
            // Root classes are unique across USS
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
