using System;
using System.IO;
using System.Text.RegularExpressions;
using Mod.Loader;
using NUnit.Framework;
using Server.Boot.Localization;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.TrainStation.Naming
{
    public class ModEnglishContentNameLoaderTest
    {
        private string _directory;
        private const string ItemKey = "item.11111111-0000-0000-0000-000000000001.name";

        [SetUp]
        public void Setup()
        {
            _directory = Path.Combine(Path.GetTempPath(), "station-names-" + Guid.NewGuid());
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void Cleanup()
        {
            Directory.Delete(_directory, true);
        }

        [Test]
        public void LoadsOnlyNonemptyEnglishContentNames()
        {
            // UI文言と空欄は命名辞書に入れない
            // Exclude UI strings and empty translations from the naming dictionary
            CreateMod("one", "one", $"key,Source,english,japanese\n{ItemKey},鉄,Iron,鉄\nfluid.test.name,水,Water,水\nui.foo,a,Foo,あ\nitem.empty.name,石,,石\n");
            var texts = ModEnglishContentNameLoader.Load(new ModsResource(_directory));
            Assert.AreEqual("Iron", texts[ItemKey]);
            Assert.AreEqual("Water", texts["fluid.test.name"]);
            Assert.AreEqual(2, texts.Count);
        }

        [Test]
        public void MasterModOrderOverridesResourceInsertionOrder()
        {
            CreateMod("first", "z", $"key,Source,english\n{ItemKey},,Later\n");
            CreateMod("second", "a", $"key,Source,english\n{ItemKey},,Earlier\n");
            var mods = new ModsResource(_directory);
            // 挿入順に依存せずmod IDの序数順で後勝ちにする
            // Later ordinal mod IDs win independently of insertion order
            var later = mods.Mods["author:z"];
            var earlier = mods.Mods["author:a"];
            mods.Mods.Clear();
            mods.Mods.Add("author:z", later);
            mods.Mods.Add("author:a", earlier);
            Assert.AreEqual("Later", ModEnglishContentNameLoader.Load(mods)[ItemKey]);
        }

        [Test]
        public void EmptyLaterTranslationKeepsEarlierName()
        {
            CreateMod("one", "a", $"key,Source,english\n{ItemKey},,Iron\n");
            CreateMod("two", "z", $"key,Source,english\n{ItemKey},,\n");
            Assert.AreEqual("Iron", ModEnglishContentNameLoader.Load(new ModsResource(_directory))[ItemKey]);
        }

        [Test]
        public void InvalidCsvIsSkippedWithError()
        {
            CreateMod("one", "a", "wrong,header\n");
            LogAssert.Expect(LogType.Error, new Regex(@"\[ModEnglishContentName\] skipped unreadable localization csv"));
            Assert.IsEmpty(ModEnglishContentNameLoader.Load(new ModsResource(_directory)));
        }

        [Test]
        public void MissingCsvAndEnglishColumnProduceNoNames()
        {
            CreateMod("one", "a", "key,Source,japanese\nitem.foo.name,鉄,鉄\n");
            CreateMod("two", "z", null);
            Assert.IsEmpty(ModEnglishContentNameLoader.Load(new ModsResource(_directory)));
        }

        private void CreateMod(string folder, string id, string csv)
        {
            var modDirectory = Path.Combine(_directory, folder);
            var masterDirectory = Path.Combine(modDirectory, "master");
            Directory.CreateDirectory(masterDirectory);
            File.WriteAllText(Path.Combine(masterDirectory, "modMeta.json"),
                $"{{\"id\":\"{id}\",\"name\":\"test\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}}");
            // CSV無しのmodも同じ入力経路で検証する
            // Verify a mod without CSV through the same input path
            if (csv == null) return;
            var localizationDirectory = Path.Combine(modDirectory, "localization");
            Directory.CreateDirectory(localizationDirectory);
            File.WriteAllText(Path.Combine(localizationDirectory, "localization.csv"), csv);
        }
    }
}
