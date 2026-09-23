using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Block.Interface;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game
{
    public class SaveLoadPreparerVersionTest
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

        [Test]
        public void 未来版のセーブは準備段階で拒否されるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 999;
            LogAssert.Expect(LogType.Error, new Regex("^セーブをロードできません"));

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsFalse(prepared.CanLoad);
            Assert.AreEqual(SaveLoadBlockedCause.FutureVersion, prepared.BlockedCause);
            StringAssert.Contains("999", prepared.BlockedReason);
            Assert.IsNull(prepared.Save);
        }

        [Test]
        public void 版0のセーブは準備段階で拒否されるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 0;
            LogAssert.Expect(LogType.Error, new Regex("^セーブをロードできません"));

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsFalse(prepared.CanLoad);
            Assert.AreEqual(SaveLoadBlockedCause.InvalidVersion, prepared.BlockedCause);
            StringAssert.Contains("版0", prepared.BlockedReason);
        }

        [TestCase("\"abc\"", TestName = "壊れたworldVersion_文字列は理由付きで拒否されるTest")]
        [TestCase("null", TestName = "壊れたworldVersion_nullは理由付きで拒否されるTest")]
        [TestCase("2147483648", TestName = "壊れたworldVersion_int超過は理由付きで拒否されるTest")]
        [TestCase("99999999999999999999", TestName = "壊れたworldVersion_long超過は理由付きで拒否されるTest")]
        public void 壊れた版のセーブは理由付きで拒否されるTest(string worldVersionJson)
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = JToken.Parse(worldVersionJson);
            LogAssert.Expect(LogType.Error, new Regex("worldVersionが整数として読めません"));
            LogAssert.Expect(LogType.Error, new Regex("^セーブをロードできません"));

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsFalse(prepared.CanLoad);
            Assert.AreEqual(SaveLoadBlockedCause.UnreadableWorldVersion, prepared.BlockedCause);
            Assert.IsNotEmpty(prepared.BlockedReason);
            Assert.IsNull(prepared.Save);
        }

        [Test]
        public void 現在版のセーブも欠損ブロックの除去前に原本を退避するTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["world"]).Add(SaveLoadPreparerTestFixture.MissingBlock(987663, 83));
            var originalText = save.ToString();

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            preparer.Prepare(originalText);

            var archive = WorldDataDirectory.FromWorldRoot(_archiveRoot);
            Assert.AreEqual(originalText, File.ReadAllText(archive.BackupSaveJsonPath(WorldSaveAllInfo.CurrentVersion)));
            Assert.IsFalse(File.Exists(archive.BackupSaveJsonPath(1)), "現在版のセーブが版1として退避されています");
        }

        [Test]
        public void 版1のセーブは試作境界で拒否されるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;
            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            ExpectPrototypeRefusal();
            var prepared = preparer.Prepare(save.ToString());
            Assert.IsFalse(prepared.CanLoad);
            StringAssert.Contains("new-world belt segment prototype", prepared.BlockedReason);
        }

        [Test]
        public void 現在版のセーブは原文のまま退避され2回目で上書きされないTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var originalText = save.ToString();
            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            preparer.Prepare(originalText);
            var backupPath = WorldDataDirectory.FromWorldRoot(_archiveRoot).BackupSaveJsonPath(WorldSaveAllInfo.CurrentVersion);
            Assert.AreEqual(originalText, File.ReadAllText(backupPath));
            var second = JObject.Parse(originalText);
            second["currentTick"] = 999;
            preparer.Prepare(second.ToString());
            Assert.AreEqual(originalText, File.ReadAllText(backupPath));
        }

        [Test]
        public void 未来版のセーブでは起動が中断され新規ワールドが作られないTest()
        {
            var saveJsonFilePath = Path.Combine(_archiveRoot, "save.json");
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 999;
            Directory.CreateDirectory(_archiveRoot);
            File.WriteAllText(saveJsonFilePath, save.ToString());

            LogAssert.Expect(LogType.Error, new Regex("^セーブをロードできません"));
            LogAssert.Expect(LogType.Error, new Regex("^セーブファイルパス"));

            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, saveJsonFilePath),
            };
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);

            var exception = Assert.Throws<Exception>(() => serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize());
            StringAssert.Contains("999", exception.Message);
        }

        [Test]
        public void 現在版のセーブはLoadOrInitializeで実際にロードできるTest()
        {
            var saveJsonFilePath = Path.Combine(_archiveRoot, "save.json");
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            Directory.CreateDirectory(_archiveRoot);
            File.WriteAllText(saveJsonFilePath, save.ToString());
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            { worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, saveJsonFilePath) };
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(options);
            Assert.DoesNotThrow(() => services.GetService<IWorldSaveDataLoader>().LoadOrInitialize());
        }

        [Test]
        public void 欠損ブロック入りのセーブはLoadOrInitializeで除去後に実ロードできるTest()
        {
            var saveJsonFilePath = Path.Combine(_archiveRoot, "save.json");
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["world"]).Add(SaveLoadPreparerTestFixture.MissingBlock(987662, 82));
            Directory.CreateDirectory(_archiveRoot);
            File.WriteAllText(saveJsonFilePath, save.ToString());

            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, saveJsonFilePath),
            };
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);

            Assert.DoesNotThrow(() => serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize());
            Assert.IsFalse(ServerContext.WorldBlockDatastore.BlockMasterDictionary.ContainsKey(new BlockInstanceId(987662)));
        }

        [Test]
        public void DIから解決した連鎖が旧セーブを試作境界で拒否するTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;
            LogAssert.Expect(LogType.Error, new Regex("変換できませんでした"));
            var result = SaveLoadPreparerTestFixture.CreateContainer().GetService<SaveMigrationChain>().Migrate(save);
            Assert.IsFalse(result.CanLoad);
            StringAssert.Contains("new-world belt segment prototype", result.BlockedReason);
        }
        private static void ExpectPrototypeRefusal()
        {
            LogAssert.Expect(LogType.Error, new Regex("変換できませんでした"));
            LogAssert.Expect(LogType.Error, new Regex("^セーブをロードできません"));
        }
    }
}
