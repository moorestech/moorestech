// moorestech_client/Assets/Scripts/Client.Tests/Ui/Conventions/UiLayerDependencyRules.cs
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// 層の参照の向きを検査（L2）
    /// Checks the layer reference direction (L2)
    /// </summary>
    public static class UiLayerDependencyRules
    {
        private sealed class LayerRule
        {
            public LayerRule(string directoryPrefix, string[] forbiddenNamespaces, string[] allowedNamespaces)
            {
                DirectoryPrefix = directoryPrefix;
                ForbiddenNamespaces = forbiddenNamespaces;
                AllowedNamespaces = allowedNamespaces;
            }

            public string DirectoryPrefix { get; }
            public string[] ForbiddenNamespaces { get; }
            public string[] AllowedNamespaces { get; }
        }

        // 層別の禁止namespaceと例外
        // Forbidden namespaces per layer and exceptions
        private static readonly LayerRule[] Rules =
        {
            new("Components/", new[] { "Client.Game", "Client.Ui.Screens", "Client.Ui.Content", "Client.Ui.State", "Core.Master", "Client.Network", "Client.Mod" }, new[] { "Client.Ui.State.Text" }),
            // マスタとMod（ID→表示素材の解決）はContentだけが読む。asmdefは同じClient.Uiなのでここで止める
            // Only Content reads masters and mods (ID-to-material resolution); they share the Client.Ui asmdef, so this rule stops it
            new("Foundation/", new[] { "Client.Ui.Components", "Client.Ui.Content", "Client.Ui.Screens", "Client.Ui.State", "Core.Master", "Client.Mod" }, new[] { "Client.Ui.State.Text", "Client.Ui.State.Routing" }),
            new("Content/", new[] { "Client.Ui.Screens" }, new string[0]),
        };

        private static readonly Regex LineComment = new(@"//[^\n]*");
        private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Singleline);
        private static readonly Regex StringLiteral = new(@"""(?:\\.|[^""\\])*""");
        private static readonly Regex QualifiedName = new(@"\b([A-Z]\w*(?:\.[A-Z]\w*)+)");

        public static List<string> FindViolations(string clientUiRelativePath, string sourceText)
        {
            var rule = Rules.FirstOrDefault(candidate => clientUiRelativePath.StartsWith(candidate.DirectoryPrefix));
            var violations = new List<string>();
            if (rule == null) return violations;

            // 本文のドット区切り名を全部見る
            // Scan every dotted name in the body
            var code = StringLiteral.Replace(BlockComment.Replace(LineComment.Replace(sourceText, ""), ""), "\"\"");
            foreach (Match match in QualifiedName.Matches(code))
            {
                var name = match.Groups[1].Value;
                var forbidden = rule.ForbiddenNamespaces.FirstOrDefault(ns => IsUnder(name, ns));
                if (forbidden == null || rule.AllowedNamespaces.Any(ns => IsUnder(name, ns))) continue;
                violations.Add($"{clientUiRelativePath}: {rule.DirectoryPrefix.TrimEnd('/')} must not reference {name}");
            }
            return violations.Distinct().ToList();

            #region Internal

            bool IsUnder(string name, string ns)
            {
                return name == ns || name.StartsWith(ns + ".");
            }

            #endregion
        }
    }
}
