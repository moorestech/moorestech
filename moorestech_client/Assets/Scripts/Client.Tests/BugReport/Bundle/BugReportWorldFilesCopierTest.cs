using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Game.Paths;
using NUnit.Framework;

namespace Client.Tests.BugReport.Bundle
{
    // 箱に入った実体と manifest の申告が一致するかの回帰ガード。食い違うと受け側は無い物を土台に起動しようとする
    // Regression guard that the manifest matches what really landed in the box; a mismatch makes the receiver boot on something absent
    public class BugReportWorldFilesCopierTest
    {
        private string _root;
        private string _worldRoot;
        private string _staged;
        private string _bundle;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "bugreport_world_copy_test_" + Path.GetRandomFileName());
            _worldRoot = Path.Combine(_root, "world_source");
            _staged = Path.Combine(_root, "staged");
            _bundle = Path.Combine(_root, "bundle");
            Directory.CreateDirectory(_worldRoot);
            Directory.CreateDirectory(_staged);
            Directory.CreateDirectory(_bundle);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        // 生成ワールドは world.json だけを入れる。地形は seed・指紋・生成器版から再現側が引き当てる（ADR 0064）
        // A generated world ships only world.json; its terrain is restored by the reproducer from seed, fingerprint and generator version (ADR 0064)
        [Test]
        public void 生成ワールドはworld_jsonだけを入れmanifestに省略を記録する()
        {
            WriteWorld("generated");
            Directory.CreateDirectory(Path.Combine(_worldRoot, "terrain"));
            File.WriteAllBytes(Path.Combine(_worldRoot, "terrain", "height_0_0.r16"), new byte[8]);
            WriteStaged("tick_5.json");

            var manifest = Copy(new List<string> { "tick_5.json" }, new List<string>());

            Assert.IsTrue(File.Exists(Path.Combine(_bundle, "world", "world.json")));
            Assert.IsFalse(File.Exists(Path.Combine(_bundle, "world", "map.json")));
            Assert.IsFalse(Directory.Exists(Path.Combine(_bundle, "world", "terrain")));
            Assert.AreEqual(BugReportBundleLayout.WorldDefinitionGeneratedWorldJsonOnly, manifest.WorldDefinition);
            var missingItems = manifest.Missing.Select(item => item.Item).ToList();
            CollectionAssert.DoesNotContain(missingItems, "terrain");
            CollectionAssert.DoesNotContain(missingItems, "map.json");
        }

        [Test]
        public void 手作りワールドはmap_jsonと地形を全部入れる()
        {
            WriteWorld("template");
            Directory.CreateDirectory(Path.Combine(_worldRoot, "terrain"));
            File.WriteAllBytes(Path.Combine(_worldRoot, "terrain", "height_0_0.r16"), new byte[8]);
            WriteStaged("tick_5.json");

            var manifest = Copy(new List<string> { "tick_5.json" }, new List<string>());

            Assert.IsTrue(File.Exists(Path.Combine(_bundle, "world", "map.json")));
            Assert.IsTrue(File.Exists(Path.Combine(_bundle, "world", "terrain", "height_0_0.r16")));
            Assert.AreEqual(BugReportBundleLayout.WorldDefinitionFull, manifest.WorldDefinition);
        }

        // world.json が無い生成前提の地形省略と違い、手作りワールドの地形は同梱が唯一の入手経路。無ければ再現できない
        // Unlike the generated omission, a hand-made world's terrain has no other source; without it reproduction is impossible
        [Test]
        public void 手作りワールドは地形が無ければ欠損に載る()
        {
            WriteWorld("template");
            WriteStaged("tick_5.json");

            var manifest = Copy(new List<string> { "tick_5.json" }, new List<string>());

            CollectionAssert.Contains(manifest.Missing.Select(item => item.Item).ToList(), "terrain");
        }

        // 地形省略の判断は読めた world.json だけに基づく。読めなければ全部入れる側に倒す（fail-closedはmapModeを省かない側）
        // The omission decision rests only on a successfully-read world.json; an unreadable one falls back to shipping everything (fail-closed means not omitting)
        [Test]
        public void world_jsonが読めなければ全部入れる側に倒し理由を残す()
        {
            File.WriteAllText(Path.Combine(_worldRoot, "world.json"), "{broken");
            File.WriteAllText(Path.Combine(_worldRoot, "map.json"), "{}");
            WriteStaged("tick_5.json");

            var manifest = Copy(new List<string> { "tick_5.json" }, new List<string>());

            Assert.IsTrue(File.Exists(Path.Combine(_bundle, "world", "map.json")));
            Assert.AreEqual(BugReportBundleLayout.WorldDefinitionFull, manifest.WorldDefinition);
            CollectionAssert.Contains(manifest.Missing.Select(item => item.Item).ToList(), "world.json");
        }

        // 退避に失敗したファイルまで載せると、受け側は存在しないスナップショットを土台にしようとする
        // Listing a file that failed to stage makes the receiving side build on a snapshot that is not there
        [Test]
        public void 箱に入らなかったスナップショットはtickにも一覧にも載らない()
        {
            WriteWorld("template");
            WriteStaged("tick_5.json");

            var manifest = Copy(new List<string> { "tick_5.json", "tick_9.json" }, new List<string>());

            CollectionAssert.AreEqual(new[] { "tick_5.json" }, manifest.SnapshotFiles);
            CollectionAssert.AreEqual(new ulong[] { 5 }, manifest.SnapshotTicks);
            CollectionAssert.Contains(manifest.Missing.Select(item => item.Item).ToList(), "tick_9.json");
        }

        // tick 0 のスナップショット（ワールド作成直後の報告）が黙って捨てられていた
        // The tick 0 snapshot (a report right after world creation) used to be dropped silently
        [Test]
        public void tick0のスナップショットも載る()
        {
            WriteWorld("template");
            WriteStaged("tick_0.json");

            var manifest = Copy(new List<string> { "tick_0.json" }, new List<string>());

            CollectionAssert.AreEqual(new ulong[] { 0 }, manifest.SnapshotTicks);
        }

        [Test]
        public void tickを読めないファイル名は理由付きで欠損に載る()
        {
            WriteWorld("template");
            WriteStaged("tick_broken.json");

            var manifest = Copy(new List<string> { "tick_broken.json" }, new List<string>());

            CollectionAssert.IsEmpty(manifest.SnapshotTicks);
            Assert.IsTrue(manifest.Missing.Any(item => item.Item == "tick_broken.json" && item.Reason.Length > 0));
        }

        [Test]
        public void 退避できていなければ理由を残して箱を作らない()
        {
            WriteWorld("template");

            var manifest = new BugReportManifest();
            BugReportWorldFilesCopier.Copy(new BugReportCapturedData { WorldRootDirectory = _worldRoot }, _bundle, manifest);

            CollectionAssert.Contains(manifest.Missing.Select(item => item.Item).ToList(), "snapshots");
            Assert.IsFalse(Directory.Exists(Path.Combine(_bundle, "snapshots")));
        }

        private BugReportManifest Copy(List<string> snapshotFileNames, List<string> packetLogFileNames)
        {
            var manifest = new BugReportManifest();
            var data = new BugReportCapturedData
            {
                StagedSnapshotDirectory = _staged,
                WorldRootDirectory = _worldRoot,
                SnapshotFileNames = snapshotFileNames,
                PacketLogFileNames = packetLogFileNames,
            };
            BugReportWorldFilesCopier.Copy(data, _bundle, manifest);
            return manifest;
        }

        private void WriteWorld(string mapMode)
        {
            File.WriteAllText(Path.Combine(_worldRoot, "world.json"), "{\"mapMode\":\"" + mapMode + "\"}");
            File.WriteAllText(Path.Combine(_worldRoot, "map.json"), "{}");
        }

        private void WriteStaged(string fileName)
        {
            File.WriteAllText(Path.Combine(_staged, fileName), "{}");
        }
    }
}
