using System.IO;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot.Replay;

namespace Tests.CombinedTest.Server.Replay
{
    // 生成ワールドの箱は world.json だけを持つ。再生ツールがそこから地形を引き当てることを固定する（ADR 0064）
    // A generated world's bundle carries only world.json; pins that the replay tool locates the terrain from it (ADR 0064)
    public class BugReportBundleWorldResolverTest
    {
        private string _root;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "bwr-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void map_jsonがある箱はその箱のworldを使う()
        {
            var bundle = Bundle(Meta("generated", "fp", "digest-a"), true);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Resolved, resolution.Outcome);
            Assert.AreEqual(Path.Combine(bundle, "world"), ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World.Root);
        }

        [Test]
        public void 生成ワールドでmap_jsonが無ければ同梱スナップショットを引き当てる()
        {
            var meta = Meta("generated", "fp", "digest-a");
            var bundle = Bundle(meta, false);
            var serverData = ServerData();
            var snapshot = WriteBundledSnapshot(serverData, meta, Meta("generated", "fp", "digest-a"));

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Resolved, resolution.Outcome);
            Assert.AreEqual(snapshot.Root, ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World.Root);
        }

        [Test]
        public void 配置台帳ダイジェストが違うスナップショットは理由付きで拒む()
        {
            var meta = Meta("generated", "fp", "digest-a");
            var bundle = Bundle(meta, false);
            var serverData = ServerData();
            WriteBundledSnapshot(serverData, meta, Meta("generated", "fp", "digest-b"));

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("placementLedgerDigest", ((BugReportBundleWorldResolution.RejectedWorld)resolution).Reason);
        }

        [Test]
        public void 同梱にも共有キャッシュにも無ければ理由付きで拒む()
        {
            // 指紋を毎回ランダムにし、実マシンの共有キャッシュに同じ worldId が偶然あっても結果が変わらないようにする
            // A random fingerprint keeps the result independent of whatever the real machine's shared cache happens to hold
            var bundle = Bundle(Meta("generated", "fp-" + Path.GetRandomFileName(), "digest-a"), false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("worldSnapshots", ((BugReportBundleWorldResolution.RejectedWorld)resolution).Reason);
        }

        [Test]
        public void 手作りワールドでmap_jsonが無い箱は拒む()
        {
            var bundle = Bundle(Meta("template", null, null), false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("mapMode=template", ((BugReportBundleWorldResolution.RejectedWorld)resolution).Reason);
        }

        [Test]
        public void 生成ワールドで指紋が無いworld_jsonは例外でなく理由付きで拒む()
        {
            var bundle = Bundle(Meta("generated", null, "digest-a"), false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("generationMasterFingerprint", ((BugReportBundleWorldResolution.RejectedWorld)resolution).Reason);
        }

        [Test]
        public void 壊れたworld_jsonは理由付きで拒む()
        {
            var bundle = Bundle(Meta("generated", "fp", "digest-a"), false);
            File.WriteAllText(WorldDataDirectory.FromWorldRoot(Path.Combine(bundle, "world")).WorldMetaFilePath, "{ not json");
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("world.json", ((BugReportBundleWorldResolution.RejectedWorld)resolution).Reason);
        }

        private static WorldMetaJson Meta(string mode, string fingerprint, string digest)
        {
            return new WorldMetaJson { Seed = 196, GeneratorVersion = "4.0.0", Algorithm = "VanillaGenerator", MapMode = mode, GenerationMasterFingerprint = fingerprint, PlacementLedgerDigest = digest, TerrainResolution = 2049, TerrainTileCount = 9 };
        }

        private string Bundle(WorldMetaJson meta, bool withMapJson)
        {
            var world = WorldDataDirectory.FromWorldRoot(Path.Combine(_root, "bundle", "world"));
            Directory.CreateDirectory(world.Root);
            File.WriteAllText(world.WorldMetaFilePath, JsonConvert.SerializeObject(meta));
            if (withMapJson) File.WriteAllText(world.MapJsonFilePath, "{}");
            return Path.Combine(_root, "bundle");
        }

        private static WorldDataDirectory WriteBundledSnapshot(string serverData, WorldMetaJson bundleMeta, WorldMetaJson snapshotMeta)
        {
            var worldId = WorldIdentity.CalculateGenerated(bundleMeta.Seed, bundleMeta.GenerationMasterFingerprint, bundleMeta.GeneratorVersion);
            var snapshot = WorldDataDirectory.ForBundledSnapshot(serverData, worldId);
            Directory.CreateDirectory(snapshot.Root);
            File.WriteAllText(snapshot.WorldMetaFilePath, JsonConvert.SerializeObject(snapshotMeta));
            File.WriteAllText(snapshot.MapJsonFilePath, "{}");
            return snapshot;
        }

        private string ServerData()
        {
            var dir = Path.Combine(_root, "serverData");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
