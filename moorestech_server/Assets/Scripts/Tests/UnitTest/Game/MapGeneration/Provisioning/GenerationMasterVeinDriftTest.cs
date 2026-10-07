using System;
using System.Text.RegularExpressions;
using Game.Map.Interface.Json;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Provisioning
{
    // 鉱脈を整地する版では、保存鉱脈と再生成鉱脈の集合一致もマスタ変更の再解決条件になることを検証する
    // Verifies that for revisions grading around veins, the saved and regenerated vein sets must also agree before a master drift is resolved
    [Category("CiShardServerMap1")]
    public class GenerationMasterVeinDriftTest
    {
        private GeneratedWorldProvisionFixture _fixture;
        private WorldDataDirectory _worldDataDirectory;

        [SetUp]
        public void SetUp()
        {
            _fixture = new GeneratedWorldProvisionFixture(nameof(GenerationMasterVeinDriftTest));
            _worldDataDirectory = _fixture.WorldDataDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.DeleteWorld();
        }

        // mapObjectが0件のままでも鉱脈だけが動けば、台帳padは旧鉱脈と別の位置を整地するので記録を進めない
        // Even with zero map objects, moved veins alone would make ledger pads grade away from the old veins, so the record never advances
        [Test]
        public void Grounded5で鉱脈だけが食い違う既存ワールドは記録を進めず例外を投げる()
        {
            var settings = _fixture.ProvisionGeneratedWorld();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;
            var originalWorldMeta = _fixture.ReadWorldMeta();
            Assert.That(originalWorldMeta.GeneratorVersion, Is.EqualTo("5.0.0"), "前提: 現行版はGrounded5");
            Assert.That(_fixture.ReadMapInfo().MapObjects, Is.Empty, "前提: ForUnitTest modはmapObjectを生成しない");

            AppendVeinNoMasterGenerates();
            _fixture.WriteFingerprintAndGeneratorVersion("tampered-fingerprint", originalWorldMeta.GeneratorVersion);

            LogAssert.Expect(LogType.Error, new Regex(@"\[GeneratedSurface\] seed=12345 revision=Grounded5 tile=all: .*\(veinGuid, min, max\) set"));
            var thrownException = Assert.Throws<InvalidOperationException>(() => WorldProvisioner.EnsureWorld(settings));
            Assert.That(thrownException.Message, Does.Contain("(veinGuid, min, max) set"));

            var rejectedWorldMeta = _fixture.ReadWorldMeta();
            Assert.AreEqual("tampered-fingerprint", rejectedWorldMeta.GenerationMasterFingerprint);
            Assert.AreEqual(originalWorldMeta.PlacementLedgerDigest, rejectedWorldMeta.PlacementLedgerDigest);
            GeneratedWorldProvisionFixture.DeleteSharedWorldCache(worldId);
        }

        // 旧版ワールドは鉱脈を整地しないので、鉱脈集合の照合は掛けず従来どおり記録を進める
        // Legacy worlds never grade around veins, so no vein-set check applies and the record advances as before
        [Test]
        public void Legacy4では鉱脈だけが食い違っても従来どおり記録を進める()
        {
            var settings = _fixture.ProvisionGeneratedWorld();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;

            AppendVeinNoMasterGenerates();
            _fixture.WriteFingerprintAndGeneratorVersion("tampered-fingerprint", "4.0.0");
            WorldProvisioner.EnsureWorld(settings);

            var repairedWorldMeta = _fixture.ReadWorldMeta();
            Assert.AreNotEqual("tampered-fingerprint", repairedWorldMeta.GenerationMasterFingerprint);
            Assert.AreEqual("4.0.0", repairedWorldMeta.GeneratorVersion);
            Assert.AreEqual(SavedRevisionLedgerFixture.ComputeDigest(_worldDataDirectory), repairedWorldMeta.PlacementLedgerDigest);
            GeneratedWorldProvisionFixture.DeleteSharedWorldCache(worldId);
            GeneratedWorldProvisionFixture.DeleteSharedWorldCache(TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId);
        }

        // 記録側にだけ1件足す。マスタが生成しない鉱脈なので(veinGuid, min, max)集合の食い違いそのものになる
        // Add one entry to the recorded side alone; the master never generates it, so it is precisely the (veinGuid, min, max) disagreement
        private void AppendVeinNoMasterGenerates()
        {
            var mapInfoJson = _fixture.ReadMapInfo();
            mapInfoJson.MapVeins.Add(new MapVeinInfoJson
            {
                VeinGuidStr = "00000000-0000-0000-0000-00000000beef",
                MinX = 10, MinY = 20, MinZ = 30,
                MaxX = 12, MaxY = 22, MaxZ = 32,
            });
            _fixture.WriteMapInfo(mapInfoJson);
        }
    }
}
