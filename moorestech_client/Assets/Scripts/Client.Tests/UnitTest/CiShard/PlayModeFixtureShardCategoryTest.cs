using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UnitTest.CiShard
{
    // PlayMode遷移するfixtureはCIの専用shard（CiShardClient*）に載せる。分類漏れはEditMode残余shardでPlayModeとdomain reloadを起こし、
    // 残余内の後続テストへstatic状態を漏らして順序依存の緑を作る（RelativeBlockPlacePreviewTestの分類漏れで実際に起きた）
    // A fixture that enters PlayMode must sit in a dedicated CI shard (CiShardClient*); a missing category runs PlayMode and a domain reload inside the EditMode remainder shard,
    // leaking static state into later remainder tests and producing order-dependent greens (this actually happened with RelativeBlockPlacePreviewTest)
    public class PlayModeFixtureShardCategoryTest
    {
        // nameof経由にして本ファイル自身が走査に掛からないようにする
        // Built through nameof so this file itself never matches the scan
        private static readonly string EnterPlayModeToken = $"new {nameof(EnterPlayMode)}(";
        private const string DedicatedShardCategoryToken = "[Category(\"CiShardClient";

        [Test]
        public void PlayModeへ入るfixtureは専用shardのCategoryを持つ()
        {
            var testsRoot = Path.Combine(Application.dataPath, "Scripts", "Client.Tests");
            var playModeFixtures = Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(EnterPlayModeToken))
                .ToList();

            // パス解決が壊れて0件走査になっても緑になる。走査対象が居ることを先に固定する
            // A broken path resolution would scan nothing and still pass, so pin down that there is something to scan first
            Assert.That(playModeFixtures, Is.Not.Empty, $"No PlayMode fixture was found under '{testsRoot}'.");

            var offenders = playModeFixtures
                .Where(path => !File.ReadAllText(path).Contains(DedicatedShardCategoryToken))
                .ToList();
            Assert.That(offenders, Is.Empty, "PlayMode fixtures without a CiShardClient* category:\n" + string.Join("\n", offenders));
        }
    }
}
