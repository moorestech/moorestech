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
    // drift検証の実生成は1x1固定
    // Drift checks do not need grid area; they generate repeatedly, so preserve the test master's 1x1 and do not make them multi-tile
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

            // 指紋を進める書き戻しがworldIdの素材(seed/createdAt)を動かすと、同一ワールドのキャッシュが丸ごと迷子になる
            // Should the fingerprint write-back move the worldId's inputs (seed/createdAt), the very same world's cache would be orphaned wholesale
            Assert.AreEqual(terrainMeta.WorldId, TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId);

            DeleteSharedWorldCache(terrainMeta.WorldId);
        }

        // 配置が食い違えば台帳とmap.jsonは別物になる。ここだけは作り直しを促す
        // Disagreeing placements make the ledger and map.json two different worlds; only this case demands a recreation
        [Test]
        public void 配置が食い違う既存ワールドはEnsureWorldが例外を投げる()
        {
            var settings = ProvisionGeneratedWorld();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;

            // マスタを差し替える代わりに記録側へ1件足す。指紋不一致のうえで(GUID,座標)集合が食い違う状態は同じ
            // Add one entry to the recorded side instead of swapping the master; the state is the same, a fingerprint mismatch over a disagreeing (guid, position) set
            AppendMapObjectNoMasterGenerates();
            WriteTamperedFingerprint();

            var thrownException = Assert.Throws<InvalidOperationException>(() => WorldProvisioner.EnsureWorld(settings));

            // 原点ずれ等の別経路の例外を集合不一致と取り違えない
            // Never mistake an exception from another path, such as shifted origins, for the set disagreement
            Assert.That(thrownException.Message, Does.Contain("(guid, position) set"));

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

        // ForUnitTest modの生成マスタはどのバイオームにもobjectConfigの要素を持たず、生成ワールドのmapObjectは0件
        // The ForUnitTest mod's generation master carries no objectConfig entry in any biome, so a generated world holds zero mapObjects
        // よって記録側にだけ1件在ることが(GUID,座標)集合の食い違いそのものになる
        // One entry existing on the recorded side alone is therefore precisely the (guid, position) set disagreement
        private void AppendMapObjectNoMasterGenerates()
        {
            var mapInfoJson = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(_worldDataDirectory.MapJsonFilePath));
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
            File.WriteAllText(_worldDataDirectory.MapJsonFilePath, JsonConvert.SerializeObject(mapInfoJson, Formatting.Indented));
        }

        // 指紋以外のキーは1文字も動かさない。既定の日付解釈はcreatedAtを末尾0の落ちた別表記で書き戻す
        // Not one character outside the fingerprint may move: the default date handling rewrites createdAt with its trailing zeros trimmed
        // createdAtはworldId(seedと繋いだ文字列のハッシュ)の素材で、動くと共有キャッシュの宛先が別ワールドへ移る
        // createdAt feeds the worldId (a hash of it joined with the seed), so moving it sends the shared cache's destination to another world
        private void WriteTamperedFingerprint()
        {
            var keepDatesAsText = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
            var worldMeta = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_worldDataDirectory.WorldMetaFilePath), keepDatesAsText);
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
