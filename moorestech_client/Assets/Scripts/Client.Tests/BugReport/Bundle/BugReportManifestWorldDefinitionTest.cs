using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport.Bundle
{
    // manifest.worldDefinition のワイヤの語を固定する。再現側と取り込み側はこの語で箱の world/ の中身を判定する（ADR 0064）
    // Pins the wire words of manifest.worldDefinition; the reproducer and the ingest side judge the box's world/ by these words (ADR 0064)
    public class BugReportManifestWorldDefinitionTest
    {
        [TestCase(BugReportWorldDefinition.Full, "full")]
        [TestCase(BugReportWorldDefinition.GeneratedWorldJsonOnly, "generated-world-json-only")]
        [TestCase(BugReportWorldDefinition.NotCaptured, "not-captured")]
        public void worldDefinitionは契約の語で書かれる(BugReportWorldDefinition definition, string expectedWireWord)
        {
            var manifest = new BugReportManifest();
            manifest.WorldDefinition = definition;

            Assert.AreEqual(expectedWireWord, (string)JObject.Parse(manifest.ToJson())["worldDefinition"]);
        }

        // 前回異常終了の箱のようにワールドを取り込まない経路は、null でなく not-captured を名乗る
        // A path that never captures the world, such as the previous-crash box, says not-captured rather than null
        [Test]
        public void ワールドを取り込まなかった箱はnot_capturedを名乗る()
        {
            Assert.AreEqual("not-captured", (string)JObject.Parse(new BugReportManifest().ToJson())["worldDefinition"]);
        }

        [Test]
        public void 記録時のワールドが分からなければnot_capturedのまま欠損を残す()
        {
            var bundle = Path.Combine(Path.GetTempPath(), "bugreport_world_definition_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(bundle);
            var manifest = new BugReportManifest();

            BugReportWorldFilesCopier.Copy(new BugReportCapturedData { WorldRootDirectory = "" }, bundle, manifest);
            Directory.Delete(bundle, true);

            Assert.AreEqual(BugReportWorldDefinition.NotCaptured, manifest.WorldDefinition);
            Assert.IsTrue(manifest.Missing.Exists(item => item.Item == BugReportBundleLayout.WorldDirectoryName));
        }
    }
}
