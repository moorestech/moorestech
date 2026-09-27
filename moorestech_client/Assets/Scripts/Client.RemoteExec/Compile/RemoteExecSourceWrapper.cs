using System.Text;
using System.Text.RegularExpressions;

namespace Client.RemoteExec.Compile
{
    // 先頭の名前空間指定を移してメソッド本体を実行用クラスへ包む
    // Lift leading namespace imports and wrap the method body in a runnable class
    public static class RemoteExecSourceWrapper
    {
        public const string EntryTypeName = "RemoteExecSnippet";
        public const string EntryMethodName = "Run";

        private static readonly string[] DefaultUsings = { "System", "System.Linq", "System.Collections.Generic", "UnityEngine", "Cysharp.Threading.Tasks" };
        private static readonly Regex UsingDirective = new Regex(@"^\s*using\s+(?:static\s+)?(?:global::)?[A-Za-z_][\w.]*(?:\s*=\s*(?:global::)?[A-Za-z_][\w.]*(?:<\s*[\w.,<>\s]+>)?)?\s*;\s*(?://.*)?$", RegexOptions.Compiled);

        internal static string Wrap(string body)
        {
            var lines = body.Replace("\r\n", "\n").Split('\n');
            var usingCount = 0;
            while (usingCount < lines.Length &&
                   (string.IsNullOrWhiteSpace(lines[usingCount]) || UsingDirective.IsMatch(lines[usingCount])))
            {
                usingCount++;
            }

            // using 文だけを移し、空行を残して診断の行番号を維持する
            // Move only import directives and leave blank lines to preserve diagnostic positions
            var source = new StringBuilder();
            foreach (var item in DefaultUsings) source.Append("using ").Append(item).AppendLine(";");
            for (var index = 0; index < usingCount; index++)
            {
                if (UsingDirective.IsMatch(lines[index])) source.AppendLine(lines[index]);
            }
            source.Append("public static class ").Append(EntryTypeName)
                .Append(" { public static async Cysharp.Threading.Tasks.UniTask<object> ")
                .Append(EntryMethodName).AppendLine("() {");
            source.AppendLine("#line 1");
            for (var index = 0; index < lines.Length; index++)
                source.AppendLine(index < usingCount ? string.Empty : lines[index]);

            // 値を返さないコードにも共通の戻り値を付ける
            // Add a shared return value for bodies without an explicit return
            source.AppendLine("#line default");
            source.AppendLine("#pragma warning disable CS0162, CS1998");
            source.Append("return null; } }");
            return source.ToString();
        }
    }
}
