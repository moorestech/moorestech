using System;
using System.IO;
using Core.Master;
using Game.Map.Interface.Json;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
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

        // 指紋不一致でも配置が保たれているなら指紋を進めるだけで済む。ワールドごと作り直させない
        // A fingerprint mismatch over unchanged placements only needs the fingerprint advanced, never recreating the whole world
        [Test]
        public void 指紋が不一致でも配置が同じならワールドを保ち指紋を現在値へ進める()
        {
            var settings = ProvisionGeneratedWorld();

            var terrainMeta = TerrainTransferMetaReader.Read(_worldDataDirectory);
            var generatedPayload = terrainMeta.GeneratedPayload;
            var currentFingerprint = generatedPayload.ComputeCurrentGenerationMasterFingerprint(TestModDirectory.ForUnitTestModDirectory);
            var sharedVisualDirectory = WorldDataDirectory.ForWorldCache(terrainMeta.WorldId).TerrainVisualDirectory;
            Assert.IsTrue(Directory.Exists(sharedVisualDirectory), "前提: 先焼きが共有キャッシュへ見た目を書き出している");

            WriteTamperedFingerprint();
            WorldProvisioner.EnsureWorld(settings);

            var repairedWorldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(_worldDataDirectory.WorldMetaFilePath));
            Assert.AreEqual(currentFingerprint, repairedWorldMeta.GenerationMasterFingerprint);
            // worldIdは指紋由来なので、指紋を現在値へ戻せば元のIDへ戻り、現在の内容に対して有効な見た目キャッシュはそのまま残る
            // The worldId derives from the fingerprint, so restoring it returns the original id and the visual cache valid for the current content stays
            Assert.AreEqual(terrainMeta.WorldId, TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId);
            Assert.IsTrue(Directory.Exists(sharedVisualDirectory), "現在の内容IDの見た目キャッシュは有効なので残る");

            DeleteSharedWorldCache(terrainMeta.WorldId);
        }

        // 配置が食い違えば台帳とmap.jsonは別物になる。ここだけは作り直しを促す
        // Disagreeing placements make the ledger and map.json two different worlds; only this case demands a recreation
        [Test]
        public void 配置が食い違う既存ワールドはEnsureWorldが例外を投げる()
        {
            var settings = ProvisionGeneratedWorld();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;

            // マスタを差し替える代わりに記録側へ1件足す。指紋不一致のうえで(GUID,座標,scale)集合が食い違う状態は同じ
            // Add one entry to the recorded side instead of swapping the master; the state is the same, a fingerprint mismatch over a disagreeing (guid, position, scale) set
            AppendMapObjectNoMasterGenerates();
            WriteTamperedFingerprint();

            var thrownException = Assert.Throws<InvalidOperationException>(() => WorldProvisioner.EnsureWorld(settings));

            // 原点ずれ等の別経路の例外を集合不一致と取り違えない
            // Never mistake an exception from another path, such as shifted origins, for the set disagreement
            Assert.That(thrownException.Message, Does.Contain("(guid, position, scale) set"));

            DeleteSharedWorldCache(worldId);
        }

        // 位置にだけ許す1mm丸めをscaleへ流用すると、見た目を変える微差なのに旧mapと新digestの組合せを記録してしまう
        // Reusing position's 1mm rounding for scale would record an old map beside a new digest despite a visible scale-only change
        [Test]
        public void Scaleだけが0_001未満動いた既存ワールドは記録を進めず例外を投げるTest()
        {
            const float originalScale = 1f;
            const float changedScale = 1.0001f;
            TestGenerationConfigFactory.LoadMasterWithMapObjectScaleForProvisioning(originalScale);
            var settings = ProvisionGeneratedWorldWithLoadedMaster();
            var originalWorldMeta = ReadWorldMeta();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;

            TestGenerationConfigFactory.LoadMasterWithMapObjectScaleForProvisioning(changedScale);
            var thrownException = Assert.Throws<InvalidOperationException>(() => WorldProvisioner.EnsureWorld(settings));
            var rejectedWorldMeta = ReadWorldMeta();

            Assert.That(thrownException.Message, Does.Contain("(guid, position, scale) set"));
            Assert.AreEqual(originalWorldMeta.GenerationMasterFingerprint, rejectedWorldMeta.GenerationMasterFingerprint);
            Assert.AreEqual(originalWorldMeta.PlacementLedgerDigest, rejectedWorldMeta.PlacementLedgerDigest);
            DeleteSharedWorldCache(worldId);
        }

        // 見た目だけが動いたときも、次の接続で使う台帳digestを現在値へ進めないとクライアントがfail-closedで開けなくなる
        // When only the visuals moved, the ledger digest must advance too or the next client connection fails closed and the world never opens
        [Test]
        public void 見た目だけが動いたマスタでは配置を保ったまま台帳digestも現在値へ進む()
        {
            TestGenerationConfigFactory.LoadMasterWithMapObjectSurroundEffectForProvisioning("rockNoBareGround");
            var settings = ProvisionGeneratedWorldWithLoadedMaster();
            var originalWorldMeta = ReadWorldMeta();
            var worldId = TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId;

            TestGenerationConfigFactory.LoadMasterWithMapObjectSurroundEffectForProvisioning("rockBareGround");
            WorldProvisioner.EnsureWorld(settings);

            var repairedWorldMeta = ReadWorldMeta();
            Assert.AreNotEqual(originalWorldMeta.PlacementLedgerDigest, repairedWorldMeta.PlacementLedgerDigest, "見た目が動けば台帳digestも動く");
            Assert.AreEqual(CurrentPlacementLedgerDigest(), repairedWorldMeta.PlacementLedgerDigest);

            DeleteSharedWorldCache(worldId);
            DeleteSharedWorldCache(TerrainTransferMetaReader.Read(_worldDataDirectory).WorldId);
        }

        // 現在のマスタでpass-1を回し直したときの台帳digest。解決側と同じ組み立てを通す
        // The ledger digest of a pass-1 re-run under the current master, assembled exactly as the resolver does
        private string CurrentPlacementLedgerDigest()
        {
            var terrainMeta = TerrainTransferMetaReader.Read(_worldDataDirectory);
            var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
            var config = MapGenerationPipeline.BuildConfigWithSettledOrigins(
                selectedGeneration, terrainMeta.WorldSeed, TestModDirectory.ForUnitTestModDirectory, terrainMeta.GeneratedPayload.Origins);
            return MapGenerationPipeline.Generate(selectedGeneration, config).Ledger.ComputeDigest();
        }

        // generated modeはMasterHolder.GenerationMaster.SelectedGenerationを要求するため、ForUnitTest modをDIコンテナ生成経由でロードする
        // generated mode requires MasterHolder.GenerationMaster.SelectedGeneration, so the ForUnitTest mod is loaded via DI container generation
        private WorldProvisionSettings ProvisionGeneratedWorld()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            return ProvisionGeneratedWorldWithLoadedMaster();
        }

        private WorldProvisionSettings ProvisionGeneratedWorldWithLoadedMaster()
        {
            var settings = new WorldProvisionSettings(_worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, WorldMapMode.Generated, 12345);
            WorldProvisioner.EnsureWorld(settings);
            return settings;
        }

        private WorldMetaJson ReadWorldMeta()
        {
            return JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(_worldDataDirectory.WorldMetaFilePath));
        }

        // ForUnitTest modの生成マスタはどのバイオームにもobjectConfigの要素を持たず、生成ワールドのmapObjectは0件
        // The ForUnitTest mod's generation master carries no objectConfig entry in any biome, so a generated world holds zero mapObjects
        // よって記録側にだけ1件在ることが(GUID,座標,scale)集合の食い違いそのものになる
        // One entry existing on the recorded side alone is therefore precisely the (guid, position, scale) set disagreement
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
