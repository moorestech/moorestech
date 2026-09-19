using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.CiShard
{
    // PlayModeへ入るテストメソッドは専用shard（CiShardClient*）に載せる。分類漏れはEditMode残余shardでdomain reloadを起こし、後続テストへstatic状態を漏らして順序依存の緑を作る
    // 判定はshard filterと同じNUnit属性を見る。ソース走査は「どのメソッドがPlayModeへ入るか」の特定にだけ使い、コメントや同居クラスの文字列には騙されない
    // A test method that enters PlayMode must sit in a dedicated shard (CiShardClient*); a missing category triggers a domain reload in the EditMode remainder and leaks static state into later tests
    // The check reads the same NUnit attributes the shard filter does; the source scan only locates which methods enter PlayMode, so comments or co-located classes cannot fool it
    public class PlayModeFixtureShardCategoryTest
    {
        private const string DedicatedShardCategoryPrefix = "CiShardClient";

        // nameof経由にして本ファイル自身が走査に掛からないようにする
        // Built through nameof so this file itself never matches the scan
        private static readonly string EnterPlayModeToken = $"new {nameof(EnterPlayMode)}(";

        private static readonly Regex NamespacePattern = new(@"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline);
        // 宣言行だけに一致させ、コメント中の「class」等を拾わない
        // Only declaration lines match, so words like "class" inside comments are never picked up
        private static readonly Regex ClassPattern = new(@"^\s*(?:(?:public|internal|private|sealed|static|abstract)\s+)*class\s+(\w+)", RegexOptions.Multiline);
        private static readonly Regex UnityTestMethodPattern = new(@"^\s*(?:(?:public|private|internal|static)\s+)*IEnumerator\s+(\w+)\s*\(", RegexOptions.Multiline);

        [Test]
        public void PlayModeへ入るテストメソッドは専用shardのCategoryを持つ()
        {
            var testsRoot = Path.Combine(Application.dataPath, "Scripts", "Client.Tests");
            var testAssembly = typeof(PlayModeFixtureShardCategoryTest).Assembly;
            var playModeMethods = Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
                .SelectMany(path => FindPlayModeMethods(path, File.ReadAllText(path)))
                .ToList();

            // パス解決が壊れて0件走査になっても緑になる。走査対象が居ることを先に固定する
            // A broken path resolution would scan nothing and still pass, so pin down that there is something to scan first
            Assert.That(playModeMethods, Is.Not.Empty, $"No PlayMode test method was found under '{testsRoot}'.");

            var offenders = new List<string>();
            foreach (var (path, typeName, methodName) in playModeMethods)
            {
                // 型やメソッドを引けないのは走査の読み違い。黙って飛ばすと穴になるので違反として出す
                // Failing to resolve the type or method means the scan misread the source; skipping silently would open a hole, so it is reported
                var type = testAssembly.GetType(typeName);
                var method = type?.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                {
                    offenders.Add($"{path}: could not resolve {typeName}.{methodName}");
                    continue;
                }

                // 実効Categoryは型とメソッドの属性の和。shard filterも同じ集合で選ぶ
                // The effective categories are the union of type and method attributes, the same set the shard filter selects on
                var categories = type.GetCustomAttributes<CategoryAttribute>(true).Concat(method.GetCustomAttributes<CategoryAttribute>(true));
                if (!categories.Any(category => category.Name.StartsWith(DedicatedShardCategoryPrefix))) offenders.Add($"{path}: {typeName}.{methodName}");
            }
            Assert.That(offenders, Is.Empty, "PlayMode test methods without a CiShardClient* category:\n" + string.Join("\n", offenders));
        }

        // EnterPlayModeの出現ごとに、直前に宣言された型とIEnumeratorメソッドを持ち主として返す
        // For each EnterPlayMode occurrence, returns the type and IEnumerator method declared just before it as the owner
        private static IEnumerable<(string path, string typeName, string methodName)> FindPlayModeMethods(string path, string source)
        {
            var namespaceMatch = NamespacePattern.Match(source);
            var namespacePrefix = namespaceMatch.Success ? namespaceMatch.Groups[1].Value + "." : "";
            var found = new HashSet<string>();
            for (var index = source.IndexOf(EnterPlayModeToken); index >= 0; index = source.IndexOf(EnterPlayModeToken, index + 1))
            {
                var before = source.Substring(0, index);
                var classMatch = ClassPattern.Matches(before).Cast<Match>().LastOrDefault();
                var methodMatch = UnityTestMethodPattern.Matches(before).Cast<Match>().LastOrDefault();
                var typeName = classMatch == null ? "<no class>" : namespacePrefix + classMatch.Groups[1].Value;
                var methodName = methodMatch == null ? "<no method>" : methodMatch.Groups[1].Value;
                if (found.Add(typeName + "." + methodName)) yield return (path, typeName, methodName);
            }
        }
    }
}
