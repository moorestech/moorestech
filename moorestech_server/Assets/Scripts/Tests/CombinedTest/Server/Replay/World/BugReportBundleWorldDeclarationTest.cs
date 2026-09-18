using System.IO;
using NUnit.Framework;
using Server.Boot.Replay.World;
using static Tests.CombinedTest.Server.Replay.World.BugReportBundleWorldFixture;

namespace Tests.CombinedTest.Server.Replay.World
{
    // resolver が manifest の worldDefinition 宣言で分岐し、宣言と中身が食い違う箱を理由付きで拒むことを固定する（D9）
    // Pins that the resolver branches on the manifest's worldDefinition and rejects, with a reason, a box whose declaration contradicts its contents (D9)
    public class BugReportBundleWorldDeclarationTest
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
        public void full宣言でmap_jsonがあれば箱のworldを使う()
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), true);
            WriteManifestDeclaring(bundle, "full");

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Resolved, resolution.Outcome);
            Assert.AreEqual(Path.Combine(bundle, "world"), ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World.Root);
        }

        // full なのに map.json が無い箱は、world.json が generated でも同梱スナップショットへ逃がさない
        // A full box without map.json is not diverted to a bundled snapshot even when its world.json says generated
        [Test]
        public void full宣言でmap_jsonが無い箱は拒む()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            WriteManifestDeclaring(bundle, "full");
            var serverData = _fixture.ServerData();
            WriteBundledSnapshot(serverData, meta, Meta("generated", fingerprint, "digest-a"), true);

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("worldDefinition=full", Reason(resolution));
        }

        [Test]
        public void generated宣言でmap_jsonがある箱は拒む()
        {
            var bundle = _fixture.Bundle(Meta("generated", RandomFingerprint(), "digest-a"), true);
            WriteManifestDeclaring(bundle, "generated-world-json-only");

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("worldDefinition=generated-world-json-only", Reason(resolution));
        }

        [Test]
        public void generated宣言でmap_jsonが無ければ同梱スナップショットを引き当てる()
        {
            var fingerprint = RandomFingerprint();
            var meta = Meta("generated", fingerprint, "digest-a");
            var bundle = _fixture.Bundle(meta, false);
            WriteManifestDeclaring(bundle, "generated-world-json-only");
            var serverData = _fixture.ServerData();
            var snapshot = WriteBundledSnapshot(serverData, meta, Meta("generated", fingerprint, "digest-a"), true);

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Resolved, resolution.Outcome);
            Assert.AreEqual(snapshot.Root, ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World.Root);
        }

        // ワールドを取り込めなかったと宣言した箱は、中身に何が残っていても土台にしない
        // A box declaring its world was not captured is never used as a base, whatever its contents hold
        [Test]
        public void not_captured宣言の箱は拒む()
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), true);
            WriteManifestDeclaring(bundle, "not-captured");

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("not-captured", Reason(resolution));
        }

        // 宣言を読めない箱（未知の語・壊れた manifest・キー無し）は旧版と同じく map.json の有無で推定する
        // A box whose declaration cannot be used (unknown word, broken manifest, absent key) is inferred from map.json like an old box
        [TestCase("{\"worldDefinition\":\"future-kind\"}")]
        [TestCase("{ broken")]
        [TestCase("{\"schemaVersion\":2}")]
        public void 宣言を使えない箱はmap_jsonの有無で推定する(string manifestJson)
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), true);
            WriteManifest(bundle, manifestJson);

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Resolved, resolution.Outcome);
            Assert.AreEqual(Path.Combine(bundle, "world"), ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World.Root);
        }

        private static string Reason(BugReportBundleWorldResolution resolution)
        {
            return ((BugReportBundleWorldResolution.RejectedWorld)resolution).Reason;
        }
    }
}
