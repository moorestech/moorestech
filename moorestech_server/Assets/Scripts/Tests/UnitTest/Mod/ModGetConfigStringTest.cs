using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Mod
{
    /// <summary>
    ///     ConfigOnlyのtestConfigOnlyMod1と2をロードできるかテストするクラス
    ///     zip、ディレクトリそれぞれロードできるかチェックする
    /// </summary>
    public class ModGetConfigStringTest
    {
        private string temporaryModDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryModDirectory = Path.Combine(
                Path.GetTempPath(),
                "moorestech-mod-meta-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryModDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryModDirectory)) Directory.Delete(temporaryModDirectory, true);
        }

        [Test]
        public void 空のModIdはロード境界で拒否する()
        {
            var masterDirectory = Path.Combine(temporaryModDirectory, "broken-mod", "master");
            Directory.CreateDirectory(masterDirectory);

            // 外部modの空idを実ロード境界へ入力する
            // Feed an empty ID from an external mod into the actual load boundary
            File.WriteAllText(
                Path.Combine(masterDirectory, "modMeta.json"),
                "{\"id\":\"\",\"name\":\"Broken\",\"version\":\"1.0.0\",\"author\":\"Author\"}");

            var exception = Assert.Throws<InvalidDataException>(
                () => new ModsResource(temporaryModDirectory));
            StringAssert.Contains("id", exception.Message);
        }

        [Test]
        public void 空のModIdを持つZipもロード境界で拒否する()
        {
            var zipPath = Path.Combine(temporaryModDirectory, "broken-mod.zip");

            // folder経路と同じ不正メタをzip境界へ入力する
            // Feed the same invalid metadata into the zip boundary
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("master/modMeta.json");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("{\"id\":\"\",\"name\":\"Broken\",\"version\":\"1.0.0\",\"author\":\"Author\"}");
            }

            var exception = Assert.Throws<InvalidDataException>(
                () => new ModsResource(temporaryModDirectory));
            StringAssert.Contains("id", exception.Message);
        }

        [Test]
        public void LoadConfigTest()
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ConfigOnlyDirectory, "mods"));
            var loaded = ModJsonStringLoader.GetMasterString(modResource);
            
            Assert.AreEqual(loaded.Count, 2);
            
            var test1modId = new ModId("Test Author 1:testMod1");
            //var test1Config = loaded.Find(x => x.ModId == test1modId);
            var test1Config = loaded.FirstOrDefault(x => x.ModId.AsPrimitive() == "Test Author 1:testMod1");
            Assert.AreEqual("testItemJson1", test1Config.JsonContents[new JsonFileName("item")]);
            Assert.AreEqual("testBlockJson1", test1Config.JsonContents[new JsonFileName("block")]);
            
            var test2modId = new ModId("Test Author 2:testMod2");
            //var test2Config = loaded.Find(x => x.ModId == test2modId);
            var test2Config = loaded.FirstOrDefault(x => x.ModId.AsPrimitive() == "Test Author 2:testMod2");
            Assert.AreEqual("testMachineRecipeJson1", test2Config.JsonContents[new JsonFileName("machineRecipe")]);
            Assert.AreEqual("testCraftRecipeJson1", test2Config.JsonContents[new JsonFileName("craftRecipe")]);
        }

        [Test]
        public void Mods辞書を逆挿入してもModIdのOrdinal昇順で返す()
        {
            var modResource = new ModsResource(Path.Combine(TestModDirectory.ConfigOnlyDirectory, "mods"));
            var reverseOrderedMods = modResource.Mods
                .OrderByDescending(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();

            // 入力dictionaryを期待順と逆にして列挙順への依存を露出させる
            // Reverse dictionary insertion to expose dependence on enumeration order
            modResource.Mods.Clear();
            foreach (var pair in reverseOrderedMods)
            {
                modResource.Mods.Add(pair.Key, pair.Value);
            }

            var loaded = ModJsonStringLoader.GetMasterString(modResource);
            CollectionAssert.AreEqual(
                new[] { "Test Author 1:testMod1", "Test Author 2:testMod2" },
                loaded.Select(config => config.ModId.AsPrimitive()).ToArray());
        }
    }
}
