using System.IO;
using NUnit.Framework;
using Server.Boot.Replay.World;
using static Tests.CombinedTest.Server.Replay.World.BugReportBundleWorldFixture;

namespace Tests.CombinedTest.Server.Replay.World
{
    // 再生の候補（同梱スナップショット・共有キャッシュ）を、読み手が例外にする入力と箱と別のワールドから守ることを固定する（ADR 0064）
    // Pins that replay candidates (bundled snapshot, shared cache) are guarded against inputs the reader throws on and against worlds other than the box's (ADR 0064)
    public class BugReportBundleWorldCandidateTest
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
            StringAssert.Contains("mapMode 'Generated'", RejectedReason(resolution));
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
            StringAssert.Contains("terrain origin keys", RejectedReason(resolution));
        }

        // 読み手はタイル数から全 height の長さを読むので、数が並べられない候補・タイルが欠けた候補は使わない
        // The reader reads every height's length from the tile count, so a candidate with an unlayable count or a missing tile is skipped
        [TestCase(3, false, "terrainTileCount（3）")]
        [TestCase(0, false, "terrainTileCount（0）")]
        [TestCase(1, true, "height_0_0.r16 が無い")]
        public void 候補の地形タイルが宣言と揃わなければ使われない(int candidateTileCount, bool deleteFirstTile, string expectedReason)
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var serverData = _fixture.ServerData();
            var candidateMeta = Meta("generated", fingerprint, "digest-a");
            candidateMeta.TerrainTileCount = candidateTileCount;
            var snapshot = WriteBundledSnapshot(serverData, meta, candidateMeta, true);
            if (deleteFirstTile) File.Delete(snapshot.TerrainHeightFilePath(0, 0));

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            StringAssert.Contains(expectedReason, RejectedReason(resolution));
        }

        // 置き場の名前（worldId）だけを信じない。中身から導き直した worldId が違う候補は別ワールドとして使わない
        // The directory name (worldId) alone is not trusted; a candidate whose contents derive another worldId is a different world and skipped
        [Test]
        public void 候補の中身から導いたworldIdが違えば使われない()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var serverData = _fixture.ServerData();
            var candidateMeta = Meta("generated", fingerprint, "digest-a");
            candidateMeta.Seed = meta.Seed + 1;
            WriteBundledSnapshot(serverData, meta, candidateMeta, true);

            var reason = RejectedReason(BugReportBundleWorldResolver.Resolve(bundle, serverData));
            StringAssert.Contains("worldSnapshots", reason);
            StringAssert.Contains("から導いた worldId", reason);
        }

        // 原点は worldId に含まれないが、ずれると map.json の座標と地形が食い違うので明示的に照合する
        // The origins are not part of the worldId, yet a shift misaligns map.json with the terrain, so they are matched explicitly
        [Test]
        public void 候補の地形原点が箱と違えば使われない()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            var serverData = _fixture.ServerData();
            var candidateMeta = Meta("generated", fingerprint, "digest-a");
            candidateMeta.TerrainSceneOriginX = 64f;
            WriteBundledSnapshot(serverData, meta, candidateMeta, true);

            var reason = RejectedReason(BugReportBundleWorldResolver.Resolve(bundle, serverData));
            StringAssert.Contains("worldSnapshots", reason);
            StringAssert.Contains("地形原点が箱の world.json と一致しない", reason);
        }
    }
}
