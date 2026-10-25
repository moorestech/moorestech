using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Orbit;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json.WorldVersions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public class RocketLaunchHistorySaveLoadTest
    {
        private string _archiveRoot;

        [SetUp]
        public void SetUp()
        {
            _archiveRoot = SaveLoadPreparerTestFixture.ArchiveRootForThisRun();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_archiveRoot)) Directory.Delete(_archiveRoot, true);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 初打ち上げ履歴は新しいDIへのセーブロードで保持される(bool hasLaunchedOnce)
        {
            var saveProvider = SaveLoadPreparerTestFixture.CreateContainer();
            var history = saveProvider.GetRequiredService<RocketLaunchHistory>();
            if (hasLaunchedOnce) history.RecordLaunch();
            var json = SaveLoadJsonTestHelper.AssembleSaveJson(saveProvider);
            var save = JObject.Parse(json);
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, save["worldVersion"].Value<int>());
            Assert.AreEqual(JTokenType.Boolean, save["rocketHasLaunchedOnce"].Type);
            Assert.AreEqual(hasLaunchedOnce, save["rocketHasLaunchedOnce"].Value<bool>());

            // 逆の状態からロードし、既定値の一致による見逃しを防ぐ
            // Load over the opposite state so a matching default cannot hide a restore omission
            var loadProvider = SaveLoadPreparerTestFixture.CreateContainer();
            var loadedHistory = loadProvider.GetRequiredService<RocketLaunchHistory>();
            loadedHistory.LoadSaveData(!hasLaunchedOnce);
            SaveLoadJsonTestHelper.LoadFromJson(loadProvider, json);

            Assert.AreNotSame(history, loadedHistory);
            Assert.AreEqual(hasLaunchedOnce, loadedHistory.HasLaunchedOnce);
        }

        [Test]
        public void 版6は本番ロード経路で未打ち上げへ移行し原本を保持する()
        {
            var saveProvider = SaveLoadPreparerTestFixture.CreateContainer();
            saveProvider.GetRequiredService<RocketLaunchHistory>().RecordLaunch();
            var save = JObject.Parse(SaveLoadJsonTestHelper.AssembleSaveJson(saveProvider));
            save.Remove("rocketHasLaunchedOnce");
            save["worldVersion"] = 6;

            // 実ファイルで退避・移行・刈込・復元を通す
            // Exercise archival, migration, pruning and restoration with a real file
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = WorldDataDirectory.FromWorldRoot(_archiveRoot).SaveJsonFilePath;
            var original = save.ToString();
            File.WriteAllText(sourcePath, original);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, sourcePath),
            };
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            var loadedHistory = loadProvider.GetRequiredService<RocketLaunchHistory>();
            loadedHistory.RecordLaunch();
            loadProvider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

            // 移行前の原本と退避を保ち、再保存には現在版のfalseが出る
            // Preserve the original and archive, and re-save false in the current version
            Assert.IsFalse(loadedHistory.HasLaunchedOnce);
            Assert.AreEqual(original, File.ReadAllText(sourcePath));
            Assert.AreEqual(original, File.ReadAllText(loadProvider.GetRequiredService<WorldDataDirectory>().BackupSaveJsonPath(6)));
            var resaved = JObject.Parse(SaveLoadJsonTestHelper.AssembleSaveJson(loadProvider));
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, resaved["worldVersion"].Value<int>());
            Assert.IsFalse(resaved["rocketHasLaunchedOnce"].Value<bool>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 現在版で履歴が欠損かnullなら復元せず理由付きで拒否する(bool explicitNull)
        {
            var provider = SaveLoadPreparerTestFixture.CreateContainer();
            var history = provider.GetRequiredService<RocketLaunchHistory>();
            history.RecordLaunch();
            var save = JObject.Parse(SaveLoadJsonTestHelper.AssembleSaveJson(provider));
            if (explicitNull) save["rocketHasLaunchedOnce"] = JValue.CreateNull();
            else save.Remove("rocketHasLaunchedOnce");
            LogAssert.Expect(LogType.Error, new Regex("^セーブに rocketHasLaunchedOnce がありません"));

            // 欠損をfalseへ化かさず、ロード前の状態を保って中断する
            // Do not turn absence into false; abort while preserving the pre-load state
            var error = Assert.Throws<InvalidOperationException>(() => SaveLoadJsonTestHelper.LoadFromJson(provider, save.ToString()));

            StringAssert.Contains("rocketHasLaunchedOnce", error.Message);
            Assert.IsTrue(history.HasLaunchedOnce);
        }
    }
}
