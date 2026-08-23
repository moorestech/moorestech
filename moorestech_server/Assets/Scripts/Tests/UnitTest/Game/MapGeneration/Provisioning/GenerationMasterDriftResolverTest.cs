using System;
using System.IO;
using Game.Map.Interface.Json;
using Game.MapGeneration.Export;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration.Provisioning
{
    // 生成マスタがワールド作成時から動いたときの解決を、EnsureWorldの既存ワールド判定を通して検証する
    // Verifies how a generation master that moved since world creation is resolved, through EnsureWorld's existing-world branch
    public class GenerationMasterDriftResolverTest
    {
        private WorldDataDirectory _worldDataDirectory;

        [SetUp]
        public void SetUp()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), "GenerationMasterDriftResolverTest_" + Guid.NewGuid());
            _worldDataDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_worldDataDirectory.Root)) Directory.Delete(_worldDataDirectory.Root, true);
            if (Directory.Exists(_worldDataDirectory.ProvisioningTempDirectory)) Directory.Delete(_worldDataDirectory.ProvisioningTempDirectory, true);
        }

        // 指紋不一致でも配置が保たれているなら見た目を焼き直せば済む。ワールドごと作り直させない
        // A fingerprint mismatch over unchanged placements only needs the visuals rebaked, never recreating the whole world
        [Test]
        public void 指紋が不一致でも配置が同じならワールドを保ち指紋を現在値へ進める()
        {
            var settings = ProvisionGeneratedWorld();

            var terrainMeta = TerrainTransferMetaReader.Read(_worldDataDirectory);
            var currentFingerprint = terrainMeta.ComputeCurrentGenerationMasterFingerprint(TestModDirectory.ForUnitTestModDirectory);
            var sharedVisualDirectory = WorldDataDirectory.ForWorldCache(terrainMeta.WorldId).TerrainVisualDirectory;
            Assert.IsTrue(Directory.Exists(sharedVisualDirectory), "前提: 先焼きが共有キャッシュへ見た目を書き出している");

            WriteTamperedFingerprint();
            WorldProvisioner.EnsureWorld(settings);

            var repairedWorldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(_worldDataDirectory.WorldMetaFilePath));
            Assert.AreEqual(currentFingerprint, repairedWorldMeta.GenerationMasterFingerprint);
            Assert.IsFalse(Directory.Exists(sharedVisualDirectory), "見た目キャッシュは捨てて焼き直させる");

            DeleteSharedWorldCache(terrainMeta.WorldId);
        }

        // 配置が動いていたら台帳とmap.jsonが別物になる。ここだけは作り直しを促す
        // Moved placements make the ledger and map.json two different worlds; only this case demands a recreation
        [Test]
        public void 配置が動いた既存ワールドはEnsureWorldが例外を投げる()
        {
            var settings = ProvisionGeneratedWorld();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;

            // マスタを差し替える代わりに記録側を動かす。指紋不一致のうえで(GUID,座標)集合が食い違う状態は同じ
            // Move the recorded side instead of swapping the master; the state is the same, a fingerprint mismatch over a disagreeing (guid, position) set
            var mapInfoJson = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(_worldDataDirectory.MapJsonFilePath));
            Assert.That(mapInfoJson.MapObjects, Is.Not.Empty, "前提: 生成ワールドがmapObjectを持っている");
            mapInfoJson.MapObjects[0].X += 10f;
            File.WriteAllText(_worldDataDirectory.MapJsonFilePath, JsonConvert.SerializeObject(mapInfoJson, Formatting.Indented));
            WriteTamperedFingerprint();

            Assert.Throws<InvalidOperationException>(() => WorldProvisioner.EnsureWorld(settings));

            DeleteSharedWorldCache(worldId);
        }

        // generated modeはMasterHolder.GenerationMaster.SelectedGenerationを要求するため、ForUnitTest modをDIコンテナ生成経由でロードする
        // generated mode requires MasterHolder.GenerationMaster.SelectedGeneration, so the ForUnitTest mod is loaded via DI container generation
        private WorldProvisionSettings ProvisionGeneratedWorld()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var settings = new WorldProvisionSettings(_worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, WorldMapMode.Generated, 12345);
            WorldProvisioner.EnsureWorld(settings);
            return settings;
        }

        private void WriteTamperedFingerprint()
        {
            var worldMeta = JObject.Parse(File.ReadAllText(_worldDataDirectory.WorldMetaFilePath));
            worldMeta["generationMasterFingerprint"] = "tampered-fingerprint";
            File.WriteAllText(_worldDataDirectory.WorldMetaFilePath, worldMeta.ToString());
        }

        // 共有キャッシュはワールドディレクトリの外なのでTearDownの対象外。worldIdが分かるテストが自分で片付ける
        // The shared cache lives outside the world directory and escapes TearDown, so a test that knows the worldId cleans it up itself
        private static void DeleteSharedWorldCache(string worldId)
        {
            var sharedCacheRoot = WorldDataDirectory.ForWorldCache(worldId).Root;
            if (Directory.Exists(sharedCacheRoot)) Directory.Delete(sharedCacheRoot, true);
        }
    }
}
