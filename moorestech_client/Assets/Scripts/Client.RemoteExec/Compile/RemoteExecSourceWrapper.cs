using System.Text;
using System.Text.RegularExpressions;

namespace Client.RemoteExec.Compile
{
    // 先頭の名前空間指定を移してメソッド本体を実行用クラスへ包む
    // Lift leading namespace imports and wrap the method body in a runnable class
    internal static class RemoteExecSourceWrapper
    {
        internal const string EntryTypeName = "RemoteExecSnippet";
        internal const string EntryMethodName = "Run";

        private static readonly string[] DefaultUsings = { "System", "System.Linq", "System.Collections.Generic", "UnityEngine", "Cysharp.Threading.Tasks" };
        private static readonly Regex UsingDirective = new Regex(@"^\s*using\s+(?:static\s+)?(?:global::)?[A-Za-z_][\w.]*(?:\s*=\s*(?:global::)?[A-Za-z_][\w.]*(?:<\s*[\w.,<>\s]+>)?)?\s*;\s*(?://.*)?$", RegexOptions.Compiled);

        internal static string Wrap(string body)
        {
            var lines = body.Replace("\r\n", "\n").Split('\n');
            var usingCount = 0;
            var importLines = new bool[lines.Length];
            var inBlockComment = false;
            while (usingCount < lines.Length && IsLeadingTriviaOrUsing(lines[usingCount]))
            {
                usingCount++;
            }

            // using 文だけを移し、空行を残して診断の行番号を維持する
            // Move only import directives and leave blank lines to preserve diagnostic positions
            var source = new StringBuilder();
            foreach (var item in DefaultUsings) source.Append("using ").Append(item).AppendLine(";");
            for (var index = 0; index < usingCount; index++)
            {
                if (importLines[index]) source.AppendLine(lines[index]);
            }
            source.Append("public static class ").Append(EntryTypeName)
                .Append(" { public static async Cysharp.Threading.Tasks.UniTask<object> ")
                .Append(EntryMethodName).AppendLine("() {");
            source.AppendLine("#line 1");
            for (var index = 0; index < lines.Length; index++)
                source.AppendLine(index < usingCount && importLines[index] ? string.Empty : lines[index]);

            // 戻り値の型を常に統一
            // Always unify the return type
            source.AppendLine("#line default");
            source.AppendLine("#pragma warning disable CS0162, CS1998");
            source.Append("return null; } }");
            return source.ToString();

            #region Internal

            // 先頭コメントを越えて using を探す。ブロックコメント内の文字列は宣言として扱わない
            // Scan past leading comments for imports; text inside a block comment is never a directive
            bool IsLeadingTriviaOrUsing(string line)
            {
                var trimmed = line.Trim();
                if (inBlockComment)
                {
                    var end = trimmed.IndexOf("*/", System.StringComparison.Ordinal);
                    if (end < 0) return true;
                    inBlockComment = false;
                    return IsCommentTail(trimmed.Substring(end + 2));
                }
                if (trimmed.StartsWith("/*"))
                {
                    var end = trimmed.IndexOf("*/", 2, System.StringComparison.Ordinal);
                    if (end < 0)
                    {
                        inBlockComment = true;
                        return true;
                    }
                    return IsCommentTail(trimmed.Substring(end + 2));
                }
                if (trimmed.Length == 0 || trimmed.StartsWith("//")) return true;
                importLines[usingCount] = UsingDirective.IsMatch(line);
                return importLines[usingCount];
            }

            bool IsCommentTail(string tail)
            {
                var remaining = tail.Trim();
                return remaining.Length == 0 || remaining.StartsWith("//");
            }

            #endregion
        }
    }
}
