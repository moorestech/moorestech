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
            var imports = new string[lines.Length];
            var bodyLines = (string[])lines.Clone();
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
                if (imports[index] != null) source.AppendLine(imports[index]);
            }
            source.Append("public static class ").Append(EntryTypeName)
                .Append(" { public static async Cysharp.Threading.Tasks.UniTask<object> ")
                .Append(EntryMethodName).AppendLine("() {");
            source.AppendLine("#line 1");
            for (var index = 0; index < lines.Length; index++)
                source.AppendLine(bodyLines[index]);

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
                var remaining = line.TrimStart();
                var prefix = line.Substring(0, line.Length - remaining.Length);
                if (inBlockComment)
                {
                    var end = remaining.IndexOf("*/", System.StringComparison.Ordinal);
                    if (end < 0) return true;
                    inBlockComment = false;
                    prefix += remaining.Substring(0, end + 2);
                    remaining = remaining.Substring(end + 2).TrimStart();
                }
                // 同じ行の複数コメントを抜け、残った using だけを移す
                // Skip multiple comments on one line and lift only the remaining using
                while (remaining.StartsWith("/*"))
                {
                    var end = remaining.IndexOf("*/", 2, System.StringComparison.Ordinal);
                    if (end < 0)
                    {
                        inBlockComment = true;
                        return true;
                    }
                    prefix += remaining.Substring(0, end + 2);
                    remaining = remaining.Substring(end + 2).TrimStart();
                }
                if (remaining.Length == 0 || remaining.StartsWith("//")) return true;
                if (!UsingDirective.IsMatch(remaining)) return false;
                imports[usingCount] = remaining;
                bodyLines[usingCount] = prefix;
                return true;
            }

            #endregion
        }
    }
}
