using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Client.Tests.Ui.State.Notification.Coverage
{
    /// <summary>
    /// 通知生成 API の第 1 引数を送信元の .cs から拾い、通知 id へ展開する。分類できない引数は Unclassified へ積む（無音で対象外にしない）
    /// Collects first arguments of the notification factories from sender .cs files and expands them into ids; unclassifiable arguments go to Unclassified (never silently skipped)
    /// </summary>
    internal static class NotificationServerIdScanner
    {
        private static readonly Regex FactoryCallFirstArgument = new(@"NotificationMessagePack\.Create(?:Achievement|AchievementWithItem|OperationDenied)\(\s*(\$?""[^""]*"")");
        private static readonly Regex FactoryInternalLiteral = new(@"""((?:achievement|denied|saveMigration)\.[^""]+)""");
        private static readonly Regex PlainLiteral = new(@"^""([^""{}]+)""$");
        private static readonly Regex FixedMemberInterpolation = new(@"^\$""([^""{}]+)\{(\w+)\.(\w+)\}""$");
        private static readonly Regex ReasonInterpolation = new(@"^\$""([^""{}]+)\{[^""{}]+\}""$");

        public static Dictionary<string, string> ReadSenderSources()
        {
            // repo の根は moorestech_client/Assets の 2 つ上
            // The repo root is two levels above moorestech_client/Assets
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var serverDir = Path.Combine(repoRoot, "moorestech_server", "Assets", "Scripts");
            var clientDir = Path.Combine(repoRoot, "moorestech_client", "Assets", "Scripts");
            var sources = new Dictionary<string, string>();

            foreach (var path in Directory.EnumerateFiles(serverDir, "*.cs", SearchOption.AllDirectories))
            {
                if (SegmentsOf(serverDir, path).Any(segment => segment.StartsWith("Tests"))) continue;
                sources[path] = File.ReadAllText(path);
            }

            // クライアント発の通知 id（Undo の取りこぼし等）も送信元。生成 API を呼ぶか表の enum を持つファイルだけ読む
            // Client-originated ids (e.g. undo restore skips) are senders too; read only files that call the factories or declare a table enum
            var enumNames = InterpolatedNotificationIdEnums.ByPrefix.Values.Select(entry => entry.EnumName).ToArray();
            foreach (var path in Directory.EnumerateFiles(clientDir, "*.cs", SearchOption.AllDirectories))
            {
                if (SegmentsOf(clientDir, path).Contains("Client.Tests")) continue;
                var source = File.ReadAllText(path);
                if (source.Contains("NotificationMessagePack.Create") || enumNames.Any(name => source.Contains($"enum {name}"))) sources[$"client:{path}"] = source;
            }
            return sources;

            #region Internal

            string[] SegmentsOf(string root, string path)
            {
                return Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            #endregion
        }

        public static List<string> FindEnumMembers(Dictionary<string, string> sources, string enumName)
        {
            var enumBody = new Regex($@"enum\s+{enumName}\s*\{{([^}}]*)\}}");
            foreach (var source in sources.Values)
            {
                var match = enumBody.Match(source);
                if (match.Success) return match.Groups[1].Value.Split(',').Select(member => member.Split('=')[0].Trim()).Where(member => member.Length > 0).ToList();
            }
            return null;
        }

        public static ScanResult Collect(Dictionary<string, string> sources)
        {
            var ids = new SortedSet<string>(System.StringComparer.Ordinal);
            var unclassified = new List<string>();
            foreach (var (file, source) in sources.Select(pair => (pair.Key, pair.Value)))
            {
                foreach (Match call in FactoryCallFirstArgument.Matches(source)) Classify(file, call.Groups[1].Value);

                // 生成クラスの中に直書きされた id（除去告知）も送信 id に含める
                // Ids hard-coded inside the factory class (the prune notice) are sent ids too
                if (file.EndsWith("NotificationMessagePack.cs"))
                {
                    foreach (Match literal in FactoryInternalLiteral.Matches(source)) ids.Add(literal.Groups[1].Value);
                }
            }
            return new ScanResult(ids.ToList(), unclassified);

            #region Internal

            void Classify(string file, string argument)
            {
                // 登録済み接頭辞の補間は理由値として enum を展開し、未登録で {Enum.Member} 形なら固定値として扱う
                // A registered prefix is a reason value expanded over its enum; otherwise a {Enum.Member} form is one fixed value
                var literal = PlainLiteral.Match(argument);
                var fixedMember = FixedMemberInterpolation.Match(argument);
                var reason = ReasonInterpolation.Match(argument);
                if (literal.Success) ids.Add(literal.Groups[1].Value);
                else if (reason.Success && InterpolatedNotificationIdEnums.ByPrefix.TryGetValue(reason.Groups[1].Value, out var entry))
                {
                    var members = FindEnumMembers(sources, entry.EnumName);
                    if (members == null) unclassified.Add($"{file}: {argument} (enum {entry.EnumName} not found)");
                    else foreach (var member in members.Where(member => !entry.NotSentMembers.Contains(member))) ids.Add(reason.Groups[1].Value + member);
                }
                else if (fixedMember.Success && (FindEnumMembers(sources, fixedMember.Groups[2].Value)?.Contains(fixedMember.Groups[3].Value) ?? false)) ids.Add(fixedMember.Groups[1].Value + fixedMember.Groups[3].Value);
                else unclassified.Add($"{file}: {argument}");
            }

            #endregion
        }

        internal sealed class ScanResult
        {
            public readonly IReadOnlyList<string> Ids;
            public readonly IReadOnlyList<string> Unclassified;

            public ScanResult(IReadOnlyList<string> ids, IReadOnlyList<string> unclassified)
            {
                Ids = ids;
                Unclassified = unclassified;
            }
        }
    }
}
