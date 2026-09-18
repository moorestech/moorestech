using System.IO;
using System.Text.RegularExpressions;
using Game.Paths;
using NUnit.Framework;
using Server.Boot.Replay;
using Server.Boot.Replay.World;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Server.Replay.World.BugReportBundleWorldFixture;

namespace Tests.CombinedTest.Server.Replay.World
{
    // 自動修正ランの固定ワールド起動が地形付きの土台を得られることを固定する。箱の world/ は宣言どおりのまま残す（D10）
    // Pins that the auto-fix run's fixed-world boot gets a terrain-bearing base while the box's world/ stays as declared (D10)
    public class BugReportBundleWorldMaterializerTest
    {
        private BugReportBundleWorldFixture _fixture;

        [SetUp]
        public void CreateFixture()
        {
            _fixture = new BugReportBundleWorldFixture();
        }

        [TearDown]
        public void DeleteFixture()
        {
            _fixture.Delete();
        }

        [Test]
        public void 生成ワールドの箱は同梱スナップショットの地形で実体化しsave_jsonを残す()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            WriteManifestDeclaring(bundle, "generated-world-json-only");
            var serverData = _fixture.ServerData();
            var snapshot = WriteBundledSnapshot(serverData, meta, Meta("generated", fingerprint, "digest-a"), true);
            var materialized = WorldDataDirectory.FromWorldRoot(Path.Combine(bundle, BugReportBundleLayout.MaterializedWorldDirectoryName));
            Directory.CreateDirectory(materialized.Root);
            File.WriteAllText(materialized.SaveJsonFilePath, "{\"currentTick\":1200}");

            var result = BugReportBundleTools.MaterializeWorld(bundle, serverData);

            StringAssert.StartsWith("world materialized:", result);
            Assert.AreEqual(File.ReadAllText(snapshot.MapJsonFilePath), File.ReadAllText(materialized.MapJsonFilePath));
            Assert.AreEqual(File.ReadAllText(snapshot.WorldMetaFilePath), File.ReadAllText(materialized.WorldMetaFilePath));
            Assert.IsTrue(File.Exists(Path.Combine(materialized.TerrainDirectory, "height_0_0.r16")));
            Assert.IsTrue(File.Exists(Path.Combine(materialized.TerrainDirectory, "tiles", "tile_0.bin")));
            Assert.AreEqual("{\"currentTick\":1200}", File.ReadAllText(materialized.SaveJsonFilePath));

            // 箱の world/ に map.json を足すと宣言と中身が食い違い、後の決定性検査が拒否する。実体化は別の置き場だけに書く
            // Adding map.json to the box's world/ would contradict the declaration and the later determinism check would reject it, so materialization writes elsewhere only
            Assert.IsFalse(File.Exists(Path.Combine(bundle, "world", "map.json")));
        }

        [Test]
        public void 引き当てられない箱は理由付きで拒み置き場を作らない()
        {
            var bundle = _fixture.Bundle(Meta("generated", RandomFingerprint(), "digest-a"), false);
            WriteManifestDeclaring(bundle, "generated-world-json-only");

            // 実行できない理由は開発者ログにも出る（無音の縮退にしない）
            // The refusal also reaches the developer log, never a silent degradation
            LogAssert.Expect(LogType.Error, new Regex("再現ツールを実行できません.*worldSnapshots"));
            var result = BugReportBundleTools.MaterializeWorld(bundle, _fixture.ServerData());

            StringAssert.StartsWith("ERROR:", result);
            StringAssert.Contains("worldSnapshots", result);
            Assert.IsFalse(Directory.Exists(Path.Combine(bundle, BugReportBundleLayout.MaterializedWorldDirectoryName)));
        }

        [Test]
        public void full宣言の箱は箱のworldをそのまま実体化する()
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), true);
            WriteManifestDeclaring(bundle, "full");

            var resolution = BugReportBundleWorldMaterializer.Materialize(bundle, _fixture.ServerData());

            var materialized = ResolvedWorld(resolution);
            Assert.AreEqual(Path.Combine(bundle, BugReportBundleLayout.MaterializedWorldDirectoryName), materialized.Root);
            Assert.IsTrue(File.Exists(materialized.MapJsonFilePath));
            Assert.IsTrue(File.Exists(materialized.WorldMetaFilePath));
        }
    }
}
