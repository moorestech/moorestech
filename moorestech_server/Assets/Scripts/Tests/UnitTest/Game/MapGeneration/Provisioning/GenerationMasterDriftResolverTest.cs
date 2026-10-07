using System;
using System.IO;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Map.Interface.Json;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Provisioning
{
    // 生成マスタがワールド作成時から動いたときの解決を、EnsureWorldの既存ワールド判定を通して検証する
    // Verifies how a generation master that moved since world creation is resolved, through EnsureWorld's existing-world branch
    // drift検証の実生成は1x1固定
    // Drift checks do not need grid area; they generate repeatedly, so preserve the test master's 1x1 and do not make them multi-tile
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardServerMap1")]
    public class GenerationMasterDriftResolverTest
    {
        private GeneratedWorldProvisionFixture _fixture;
        private WorldDataDirectory _worldDataDirectory;

        [SetUp]
        public void SetUp()
        {
            _fixture = new GeneratedWorldProvisionFixture(nameof(GenerationMasterDriftResolverTest));
            _worldDataDirectory = _fixture.WorldDataDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.DeleteWorld();
        }

        // 指紋不一致でも配置が保たれているなら指紋を進めるだけで済む。ワールドごと作り直させない
        // A fingerprint mismatch over unchanged placements only needs the fingerprint advanced, never recreating the whole world
        [Test]
        public void 指紋が不一致でも配置が同じならワールドを保ち指紋を現在値へ進める()
        {
            var settings = _fixture.ProvisionGeneratedWorld();

            var terrainMeta = (GeneratedTerrainTransferMeta)TerrainTransferMetaReader.Read(_worldDataDirectory);
            var generatedPayload = terrainMeta.GeneratedPayload;
            var currentFingerprint = generatedPayload.ComputeCurrentGenerationMasterFingerprint(TestModDirectory.ForUnitTestModDirectory);
            var sharedVisualDirectory = WorldDataDirectory.ForWorldCache(terrainMeta.WorldId).TerrainVisualDirectory;
            Assert.IsTrue(Directory.Exists(sharedVisualDirectory), "前提: 先焼きが共有キャッシュへ見た目を書き出している");

            WriteTamperedFingerprint();
            WorldProvisioner.EnsureWorld(settings);

            var repairedWorldMeta = _fixture.ReadWorldMeta();
            Assert.AreEqual(currentFingerprint, repairedWorldMeta.GenerationMasterFingerprint);
            // worldIdは指紋由来なので、指紋を現在値へ戻せば元のIDへ戻り、現在の内容に対して有効な見た目キャッシュはそのまま残る
            // The worldId derives from the fingerprint, so restoring it returns the original id and the visual cache valid for the current content stays
            Assert.AreEqual(terrainMeta.WorldId, TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId);
            Assert.IsTrue(Directory.Exists(sharedVisualDirectory), "現在の内容IDの見た目キャッシュは有効なので残る");

            GeneratedWorldProvisionFixture.DeleteSharedWorldCache(terrainMeta.WorldId);
        }

        // 配置が食い違えば台帳とmap.jsonは別物になる。ここだけは作り直しを促す
        // Disagreeing placements make the ledger and map.json two different worlds; only this case demands a recreation
        [Test]
        public void 配置が食い違う既存ワールドはEnsureWorldが例外を投げる()
        {
            var settings = _fixture.ProvisionGeneratedWorld();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;

            // マスタを差し替える代わりに記録側へ1件足す。指紋不一致のうえで(GUID,座標,scale)集合が食い違う状態は同じ
            // Add one entry to the recorded side instead of swapping the master; the state is the same, a fingerprint mismatch over a disagreeing (guid, position, scale) set
            AppendMapObjectNoMasterGenerates();
            WriteTamperedFingerprint();

            LogAssert.Expect(LogType.Error, new Regex(@"\[GeneratedSurface\] seed=12345 revision=Grounded5 tile=all: .*\(guid, position, scale\) set"));
            var thrownException = Assert.Throws<InvalidOperationException>(() => WorldProvisioner.EnsureWorld(settings));

            // 原点ずれ等の別経路の例外を集合不一致と取り違えない
            // Never mistake an exception from another path, such as shifted origins, for the set disagreement
            Assert.That(thrownException.Message, Does.Contain("(guid, position, scale) set"));

            GeneratedWorldProvisionFixture.DeleteSharedWorldCache(worldId);
        }

        // 位置にだけ許す1mm丸めをscaleへ流用すると、見た目を変える微差なのに旧mapと新digestの組合せを記録してしまう
        // Reusing position's 1mm rounding for scale would record an old map beside a new digest despite a visible scale-only change
        [Test]
        public void Scaleだけが0_001未満動いた既存ワールドは記録を進めず例外を投げるTest()
        {
            const float originalScale = 1f;
            const float changedScale = 1.0001f;
            TestGenerationConfigFactory.LoadMasterWithMapObjectScaleForProvisioning(originalScale);
            var settings = _fixture.ProvisionGeneratedWorldWithLoadedMaster();
            var originalWorldMeta = _fixture.ReadWorldMeta();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;

            TestGenerationConfigFactory.LoadMasterWithMapObjectScaleForProvisioning(changedScale);
            LogAssert.Expect(LogType.Error, new Regex(@"\[GeneratedSurface\] seed=12345 revision=Grounded5 tile=all: .*\(guid, position, scale\) set"));
            var thrownException = Assert.Throws<InvalidOperationException>(() => WorldProvisioner.EnsureWorld(settings));
            var rejectedWorldMeta = _fixture.ReadWorldMeta();

            Assert.That(thrownException.Message, Does.Contain("(guid, position, scale) set"));
            Assert.AreEqual(originalWorldMeta.GenerationMasterFingerprint, rejectedWorldMeta.GenerationMasterFingerprint);
            Assert.AreEqual(originalWorldMeta.PlacementLedgerDigest, rejectedWorldMeta.PlacementLedgerDigest);
            GeneratedWorldProvisionFixture.DeleteSharedWorldCache(worldId);
        }

        // 見た目だけが動いたときも、次の接続で使う台帳digestを現在値へ進めないとクライアントがfail-closedで開けなくなる
        // When only the visuals moved, the ledger digest must advance too or the next client connection fails closed and the world never opens
        [Test]
        public void 見た目だけが動いたマスタでは配置を保ったまま台帳digestも現在値へ進む()
        {
            TestGenerationConfigFactory.LoadMasterWithMapObjectSurroundEffectForProvisioning("rockNoBareGround");
            var settings = _fixture.ProvisionGeneratedWorldWithLoadedMaster();
            var originalWorldMeta = _fixture.ReadWorldMeta();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;

            TestGenerationConfigFactory.LoadMasterWithMapObjectSurroundEffectForProvisioning("rockBareGround");
            WorldProvisioner.EnsureWorld(settings);

            var repairedWorldMeta = _fixture.ReadWorldMeta();
            Assert.AreNotEqual(originalWorldMeta.PlacementLedgerDigest, repairedWorldMeta.PlacementLedgerDigest, "見た目が動けば台帳digestも動く");
            Assert.AreEqual(SavedRevisionLedgerFixture.ComputeDigest(_worldDataDirectory), repairedWorldMeta.PlacementLedgerDigest);

            GeneratedWorldProvisionFixture.DeleteSharedWorldCache(worldId);
            GeneratedWorldProvisionFixture.DeleteSharedWorldCache(TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId);
        }

        // ForUnitTest modの生成マスタはどのバイオームにもobjectConfigの要素を持たず、生成ワールドのmapObjectは0件
        // The ForUnitTest mod's generation master carries no objectConfig entry in any biome, so a generated world holds zero mapObjects
        // よって記録側にだけ1件在ることが(GUID,座標,scale)集合の食い違いそのものになる
        // One entry existing on the recorded side alone is therefore precisely the (guid, position, scale) set disagreement
        private void AppendMapObjectNoMasterGenerates()
        {
            var mapInfoJson = _fixture.ReadMapInfo();
            mapInfoJson.MapObjects.Add(new MapObjectInfoJson
            {
                InstanceId = mapInfoJson.MapObjects.Count,
                MapObjectGuidStr = "00000000-0000-0000-0000-00000000dead",
                X = 10f,
                Y = 0f,
                Z = 20f,
                RotationW = 1f,
                ScaleX = 1f,
                ScaleY = 1f,
                ScaleZ = 1f,
            });
            _fixture.WriteMapInfo(mapInfoJson);
        }

        private void WriteTamperedFingerprint()
        {
            _fixture.WriteFingerprintAndGeneratorVersion("tampered-fingerprint", _fixture.ReadWorldMeta().GeneratorVersion);
        }
    }
}
