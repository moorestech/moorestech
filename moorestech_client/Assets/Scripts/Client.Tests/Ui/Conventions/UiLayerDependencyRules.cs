// moorestech_client/Assets/Scripts/Client.Tests/Ui/Conventions/UiLayerDependencyRules.cs
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Client.Tests.Ui.Conventions
{
    /// <summary>
    /// Client.Ui の中の層（Foundation・Components・Content・Screens）の参照の向きを using と完全修飾名で検査する（L2・設計書 §1.1・A-1）
    /// Checks the reference direction between layers inside Client.Ui (Foundation, Components, Content, Screens) by usings and qualified names (L2, design §1.1, A-1)
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

        // 層ごとの禁止 namespace と、禁止の中で許す例外（State の Text・Routing）
        // Forbidden namespaces per layer, and allowed exceptions inside them (State's Text and Routing)
        private static readonly LayerRule[] Rules =
        {
            new("Components/", new[] { "Client.Game", "Client.Ui.Screens", "Client.Ui.Content", "Client.Ui.State", "Core.Master", "Client.Network" }, new[] { "Client.Ui.State.Text" }),
            new("Foundation/", new[] { "Client.Ui.Components", "Client.Ui.Content", "Client.Ui.Screens", "Client.Ui.State" }, new[] { "Client.Ui.State.Text", "Client.Ui.State.Routing" }),
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

            // コメントと文字列を除いた本文の、ドット区切りの名前を全部見る
            // Look at every dotted name in the body after removing comments and strings
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
