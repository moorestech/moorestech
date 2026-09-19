using System;
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
        public void worldDefinitionは契約の語で書かれ同じ語で読み戻せる(BugReportWorldDefinition definition, string expectedWireWord)
        {
            var manifest = new BugReportManifest();
            manifest.WorldDefinition = BugReportWorldDefinitionText.ToContractText(definition);

            Assert.AreEqual(expectedWireWord, (string)JObject.Parse(manifest.ToJson())["worldDefinition"]);
            Assert.IsTrue(BugReportWorldDefinitionText.TryParse(expectedWireWord, out var parsed));
            Assert.AreEqual(definition, parsed);
        }

        // 読み側は綴りの完全一致だけを宣言とみなす。大文字違い・数値・未知の語で推定や別の値に化けない
        // The reader accepts only the exact spelling; a case variant, a number or an unknown word never turns into another value
        [TestCase("Full")]
        [TestCase("1")]
        [TestCase("future-kind")]
        [TestCase("")]
        [TestCase(null)]
        public void 契約の語でなければ読み取らない(string text)
        {
            Assert.IsFalse(BugReportWorldDefinitionText.TryParse(text, out _));
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
            var manifest = CopyWorld("", _ => { });

            Assert.AreEqual("not-captured", manifest.WorldDefinition);
            Assert.IsTrue(manifest.Missing.Exists(item => item.Item == BugReportBundleLayout.WorldDirectoryName));
        }

        // 宣言は箱に実際に入った物から決める。world.json が入らない箱は生成か手作りかも決められず、full を名乗ると受け側が地形の無い箱で起動する
        // The declaration follows what really landed; a box without world.json cannot even tell generated from hand-made, and claiming full would boot a terrain-less box
        [Test]
        public void world_jsonが入らなければnot_capturedのまま出す()
        {
            var manifest = CopyWorld("world_source", worldRoot => File.WriteAllText(Path.Combine(worldRoot, "map.json"), "{}"));

            Assert.AreEqual("not-captured", manifest.WorldDefinition);
            Assert.IsTrue(manifest.Missing.Exists(item => item.Item == "world.json"));
        }

        [Test]
        public void 手作りワールドでmap_jsonが入らなければfullを名乗らない()
        {
            var manifest = CopyWorld("world_source", worldRoot => File.WriteAllText(Path.Combine(worldRoot, "world.json"), "{\"mapMode\":\"template\"}"));

            Assert.AreEqual("not-captured", manifest.WorldDefinition);
            Assert.IsTrue(manifest.Missing.Exists(item => item.Item == "map.json"));
        }

        // worldRootName が空なら記録時のワールドが分からない箱、そうでなければ一時ディレクトリ配下にワールドを組んで写す
        // An empty worldRootName is a box whose recording world is unknown; otherwise a world is built under a temp directory and copied
        private static BugReportManifest CopyWorld(string worldRootName, Action<string> writeWorld)
        {
            var root = Path.Combine(Path.GetTempPath(), "bugreport_world_definition_test_" + Path.GetRandomFileName());
            var bundle = Path.Combine(root, "bundle");
            var worldRoot = worldRootName == "" ? "" : Path.Combine(root, worldRootName);
            Directory.CreateDirectory(bundle);
            if (worldRoot != "")
            {
                Directory.CreateDirectory(worldRoot);
                writeWorld(worldRoot);
            }
            var manifest = new BugReportManifest();

            BugReportWorldFilesCopier.Copy(new BugReportCapturedData { WorldRootDirectory = worldRoot }, bundle, manifest);
            Directory.Delete(root, true);
            return manifest;
        }
    }
}
