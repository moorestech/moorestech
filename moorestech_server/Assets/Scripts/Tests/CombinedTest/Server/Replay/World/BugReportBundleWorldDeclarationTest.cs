using System.IO;
using Game.Paths;
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
            Assert.AreEqual(Path.Combine(bundle, "world"), ResolvedWorld(resolution).Root);
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
            StringAssert.Contains("worldDefinition=full", RejectedReason(resolution));
        }

        [Test]
        public void generated宣言でmap_jsonがある箱は拒む()
        {
            var bundle = _fixture.Bundle(Meta("generated", RandomFingerprint(), "digest-a"), true);
            WriteManifestDeclaring(bundle, "generated-world-json-only");

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            StringAssert.Contains("worldDefinition=generated-world-json-only", RejectedReason(resolution));
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
            Assert.AreEqual(snapshot.Root, ResolvedWorld(resolution).Root);
        }

        // ワールドを取り込めなかったと宣言した箱は、中身に何が残っていても土台にしない
        // A box declaring its world was not captured is never used as a base, whatever its contents hold
        [Test]
        public void not_captured宣言の箱は拒む()
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), true);
            WriteManifestDeclaring(bundle, "not-captured");

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            StringAssert.Contains("not-captured", RejectedReason(resolution));
        }

        // 推定に回すのはキーの無い旧版の箱だけ。旧版は常に全部入れていたので map.json の有無で推定する
        // Only an old box without the key falls back; old versions always shipped everything, so map.json decides
        [Test]
        public void 宣言キーの無い旧版の箱はmap_jsonの有無で推定する()
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), true);
            WriteManifest(bundle, "{\"schemaVersion\":2}");

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData());
            Assert.AreEqual(Path.Combine(bundle, "world"), ResolvedWorld(resolution).Root);
        }

        // 壊れた宣言（未知の語・大文字違い・数値・null・壊れた JSON）は推定に倒さず理由付きで拒む。推定すると宣言を書いた版の意図と違うワールドで再生しうる
        // A broken declaration (unknown word, case variant, number, null, broken JSON) is refused with a reason rather than inferred, which could replay a world the declaring version never meant
        [TestCase("{\"worldDefinition\":\"future-kind\"}", "future-kind")]
        [TestCase("{\"worldDefinition\":\"Full\"}", "Full")]
        [TestCase("{\"worldDefinition\":1}", "文字列でない")]
        [TestCase("{\"worldDefinition\":null}", "文字列でない")]
        [TestCase("{ broken", "を読めない")]
        public void 壊れた宣言の箱は推定せず拒む(string manifestJson, string expectedReason)
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), true);
            WriteManifest(bundle, manifestJson);

            var reason = RejectedReason(BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData()));
            StringAssert.Contains("worldDefinition 宣言を読めず", reason);
            StringAssert.Contains(expectedReason, reason);
        }

        [Test]
        public void manifestの無い箱は推定せず拒む()
        {
            var bundle = _fixture.Bundle(Meta("template", null, null), true);
            File.Delete(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName));

            StringAssert.Contains("manifest.json が無い", RejectedReason(BugReportBundleWorldResolver.Resolve(bundle, _fixture.ServerData())));
        }
    }
}
