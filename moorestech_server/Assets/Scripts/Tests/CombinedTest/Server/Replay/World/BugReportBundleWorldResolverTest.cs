using System.IO;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Server.Boot.Replay.World;
using static Tests.CombinedTest.Server.Replay.World.BugReportBundleWorldFixture;

namespace Tests.CombinedTest.Server.Replay.World
{
    // 生成ワールドの箱は world.json だけを持つ。再生ツールがそこから地形を引き当てることを固定する（ADR 0064）。worldDefinition を宣言しない旧版の箱（fixture の既定）は map.json の有無で推定する経路を通る
    // A generated world's bundle carries only world.json; pins that the replay tool locates the terrain from it (ADR 0064). old boxes declaring no worldDefinition (the fixture default) take the map.json inference path
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
            Assert.AreEqual(Path.Combine(bundle, "world"), ResolvedWorld(resolution).Root);
        }

        [Test]
        public void 生成ワールドでmap_jsonが無ければ同梱スナップショットを引き当てる()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var serverData = _fixture.ServerData();
            var snapshot = WriteBundledSnapshot(serverData, meta, Meta(WorldMapMode.Generated, fingerprint, "digest-a"), true);

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(snapshot.Root, ResolvedWorld(resolution).Root);
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
            StringAssert.Contains("placementLedgerDigest", RejectedReason(resolution));
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
            StringAssert.Contains("worldSnapshots", RejectedReason(resolution));
            StringAssert.Contains("terrain/ が無い", RejectedReason(resolution));
            StringAssert.DoesNotContain("map.json が無い", RejectedReason(resolution));
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
            StringAssert.Contains("worldSnapshots", RejectedReason(resolution));

            // 探すだけで実ユーザーの共有キャッシュに空ディレクトリを作らない
            // Merely searching must not create an empty directory in the real user's shared cache
            Assert.IsFalse(Directory.Exists(sharedCacheRoot));
        }

        [Test]
        public void 手作りワールドでmap_jsonが無い箱は拒む()
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            StringAssert.Contains("mapMode=template", RejectedReason(resolution));
        }

        // 生成ワールドの判定は綴りの完全一致だけ（WorldMapMode.IsGenerated）。読み手ごとに大文字小文字の扱いがずれない
        // Only the exact spelling counts as generated (WorldMapMode.IsGenerated), so readers never disagree on case
        [Test]
        public void 箱のmapModeが綴りの完全一致でなければ生成ワールドとみなさない()
        {
            var bundle = _fixture.Bundle(Meta("Generated", RandomFingerprint(), "digest-a"), false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            StringAssert.Contains("mapMode=Generated", RejectedReason(resolution));
        }

        [Test]
        public void 生成ワールドで指紋が無いworld_jsonは例外でなく理由付きで拒む()
        {
            var bundle = _fixture.Bundle(Meta("generated", null, "digest-a"), false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            StringAssert.Contains("generationMasterFingerprint", RejectedReason(resolution));
        }

        [Test]
        public void 壊れたworld_jsonは理由付きで拒む()
        {
            var bundle = _fixture.Bundle(Meta("generated", RandomFingerprint(), "digest-a"), false);
            File.WriteAllText(WorldDataDirectory.FromWorldRoot(Path.Combine(bundle, "world")).WorldMetaFilePath, "{ not json");
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            StringAssert.Contains("world.json を読めません", RejectedReason(resolution));
        }
    }
}
