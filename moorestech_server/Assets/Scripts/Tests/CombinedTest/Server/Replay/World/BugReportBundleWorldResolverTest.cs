using System.IO;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Server.Boot.Replay.World;
using static Tests.CombinedTest.Server.Replay.World.BugReportBundleWorldFixture;

namespace Tests.CombinedTest.Server.Replay.World
{
    // 生成ワールドの箱は world.json だけを持つ。再生ツールがそこから地形を引き当てることを固定する（ADR 0064）。manifest の無い箱は map.json の有無で推定する経路を通る
    // A generated world's bundle carries only world.json; pins that the replay tool locates the terrain from it (ADR 0064). Boxes without a manifest take the map.json inference path
    public class BugReportBundleWorldResolverTest
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
        public void map_jsonがある箱はその箱のworldを使う()
        {
            var bundle = _fixture.Bundle(Meta("generated", RandomFingerprint(), "digest-a"), true);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Resolved, resolution.Outcome);
            Assert.AreEqual(Path.Combine(bundle, "world"), ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World.Root);
        }

        // 箱側の mapMode は大文字小文字を無視して生成ワールドと判定する。再生が実際に読むのは候補側で、そちらは読み手の受け付ける綴りのものだけを使う
        // The bundle's mapMode is judged generated case-insensitively; replay actually reads the candidate, which is used only when spelled as the reader accepts
        [TestCase("generated")]
        [TestCase("Generated")]
        public void 生成ワールドでmap_jsonが無ければ同梱スナップショットを引き当てる(string bundleMapMode)
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta(bundleMapMode, fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var serverData = _fixture.ServerData();
            var snapshot = WriteBundledSnapshot(serverData, meta, Meta(WorldMapMode.Generated, fingerprint, "digest-a"), true);

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Resolved, resolution.Outcome);
            Assert.AreEqual(snapshot.Root, ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World.Root);
        }

        [Test]
        public void 配置台帳ダイジェストが違うスナップショットは理由付きで拒む()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var serverData = _fixture.ServerData();
            WriteBundledSnapshot(serverData, meta, Meta("generated", fingerprint, "digest-b"), true);

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("placementLedgerDigest", Reason(resolution));
        }

        [Test]
        public void 候補のterrainだけが欠けていれば使われない()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var serverData = _fixture.ServerData();
            WriteBundledSnapshot(serverData, meta, Meta("generated", fingerprint, "digest-a"), false);

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("worldSnapshots", Reason(resolution));
            StringAssert.Contains("terrain/ が無い", Reason(resolution));
            StringAssert.DoesNotContain("map.json が無い", Reason(resolution));
        }

        // 再生が読むのは候補側の world.json。TerrainTransferMetaReader が例外にする候補は理由付きで使わない
        // Replay reads the candidate's world.json; a candidate TerrainTransferMetaReader would throw on is skipped with a reason
        [Test]
        public void 候補のmapModeが読み手の綴りでなければ使われない()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var serverData = _fixture.ServerData();
            WriteBundledSnapshot(serverData, meta, Meta("Generated", fingerprint, "digest-a"), true);

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("mapMode（Generated）", Reason(resolution));
        }

        [Test]
        public void 候補に地形原点のキーが無ければ使われない()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var serverData = _fixture.ServerData();
            var candidateMeta = Meta("generated", fingerprint, "digest-a");
            candidateMeta.TerrainSceneOriginZ = null;
            WriteBundledSnapshot(serverData, meta, candidateMeta, true);

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("地形原点", Reason(resolution));
        }

        [Test]
        public void 同梱にも共有キャッシュにも無ければ理由付きで拒む()
        {
            var meta = Meta("generated", RandomFingerprint(), "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var worldId = WorldIdentity.CalculateGenerated(meta.Seed, meta.GenerationMasterFingerprint, meta.GeneratorVersion);
            var sharedCacheRoot = GameSystemPaths.GetWorldCacheDirectoryPathWithoutCreating(worldId);
            Assert.IsFalse(Directory.Exists(sharedCacheRoot));

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("worldSnapshots", Reason(resolution));

            // 探すだけで実ユーザーの共有キャッシュに空ディレクトリを作らない
            // Merely searching must not create an empty directory in the real user's shared cache
            Assert.IsFalse(Directory.Exists(sharedCacheRoot));
        }

        [Test]
        public void 手作りワールドでmap_jsonが無い箱は拒む()
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("mapMode=template", Reason(resolution));
        }

        [Test]
        public void 生成ワールドで指紋が無いworld_jsonは例外でなく理由付きで拒む()
        {
            var bundle = _fixture.Bundle(Meta("generated", null, "digest-a"), false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("generationMasterFingerprint", Reason(resolution));
        }

        [Test]
        public void 壊れたworld_jsonは理由付きで拒む()
        {
            var bundle = _fixture.Bundle(Meta("generated", RandomFingerprint(), "digest-a"), false);
            File.WriteAllText(WorldDataDirectory.FromWorldRoot(Path.Combine(bundle, "world")).WorldMetaFilePath, "{ not json");
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("world.json を読めません", Reason(resolution));
        }

        private static string Reason(BugReportBundleWorldResolution resolution)
        {
            return ((BugReportBundleWorldResolution.RejectedWorld)resolution).Reason;
        }
    }
}
