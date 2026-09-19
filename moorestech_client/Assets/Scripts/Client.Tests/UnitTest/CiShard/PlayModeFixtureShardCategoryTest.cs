using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Client.Tests.UnitTest.CiShard.SourceScan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.CiShard
{
    // PlayModeへ入るテストメソッドは専用shardに載せる。分類漏れはEditMode残余shardでdomain reloadを起こし、後続テストへstatic状態を漏らして順序依存の緑を作る
    // 判定はshard filterと同じNUnit属性を、shard filterスクリプトが持つ専用Category集合との完全一致で見る。ソース走査は「どのメソッドがPlayModeへ入るか」の特定にだけ使う
    // A test method that enters PlayMode must sit in a dedicated shard; a missing category triggers a domain reload in the EditMode remainder and leaks static state into later tests
    // The check matches the same NUnit attributes the shard filter reads, exactly against the dedicated categories in the shard filter script; the source scan only locates which methods enter PlayMode
    public class PlayModeFixtureShardCategoryTest
    {
        [Test]
        public void PlayModeへ入るテストメソッドは専用shardのCategoryを持つ()
        {
            var dedicatedCategories = DedicatedShardCategoryCatalog.Load();
            var testsRoot = Path.Combine(Application.dataPath, "Scripts", "Client.Tests");
            var testAssembly = typeof(PlayModeFixtureShardCategoryTest).Assembly;
            var scan = new PlayModeMethodScanResult();
            foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)) PlayModeMethodScanner.ScanFile(path, File.ReadAllText(path), scan);

            // 読み違いは黙って飛ばすと穴になる。持ち主を特定できない出現はそれ自体を失敗にする
            // Skipping a misread silently would open a hole, so an occurrence without a resolvable owner fails on its own
            Assert.That(scan.Misreads, Is.Empty, "EnterPlayMode occurrences whose owner method could not be identified:\n" + string.Join("\n", scan.Misreads));

            // パス解決が壊れて0件走査になっても緑になる。走査対象が居ることを先に固定する
            // A broken path resolution would scan nothing and still pass, so pin down that there is something to scan first
            Assert.That(scan.ResolvedMethods, Is.Not.Empty, $"No PlayMode test method was found under '{testsRoot}'.");

            var offenders = new List<string>();
            foreach (var resolved in scan.ResolvedMethods) CollectOffenses(resolved);
            Assert.That(offenders, Is.Empty, $"PlayMode test methods without a dedicated shard category ({string.Join(", ", dedicatedCategories)}):\n" + string.Join("\n", offenders));

            #region Internal

            void CollectOffenses(ResolvedPlayModeMethod resolved)
            {
                // 型やメソッドを引けないのは走査の読み違い。違反として出し、[UnityTest]でないメソッドへの帰属も読み違いとみなす
                // Failing to resolve the type or method means the scan misread the source; an owner without [UnityTest] is treated as a misread too
                var label = $"{resolved.Path}: {resolved.TypeFullName}.{resolved.MethodName}";
                var type = testAssembly.GetType(resolved.TypeFullName);
                if (type == null)
                {
                    offenders.Add($"{label} (type could not be resolved)");
                    return;
                }
                var unityTestMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(method => method.Name == resolved.MethodName && method.GetCustomAttribute<UnityTestAttribute>() != null)
                    .ToList();
                if (unityTestMethods.Count == 0)
                {
                    offenders.Add($"{label} (no [UnityTest] method with this name)");
                    return;
                }

                // 実効Categoryは型とメソッドの属性の和。shard filterも同じ集合で選ぶ
                // The effective categories are the union of type and method attributes, the same set the shard filter selects on
                foreach (var method in unityTestMethods)
                {
                    var categories = type.GetCustomAttributes<CategoryAttribute>(true).Concat(method.GetCustomAttributes<CategoryAttribute>(true));
                    if (!categories.Any(category => dedicatedCategories.Contains(category.Name))) offenders.Add(label);
                }
            }

            #endregion
        }
    }
}
