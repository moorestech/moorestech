using System;
using System.IO;
using System.Text.RegularExpressions;
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
    /// <summary>ロード前段のうち、版の判定・版1からの変換・原本の世代付き退避を見る</summary>
    /// <summary>Covers the pre-load stage's version judgement, the version 1 migration and the generational archiving of the original</summary>
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
            StringAssert.Contains("999", prepared.BlockedReason);
            Assert.IsNull(prepared.SaveJsonText);
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
            StringAssert.Contains("版0", prepared.BlockedReason);
        }

        // 版が整数として読めないセーブも、生の型例外ではなく理由付きの拒否で抜けること
        // A save whose version is not an integer must exit through a reasoned rejection, not a bare cast exception
        [TestCase("\"abc\"", TestName = "壊れたworldVersion_文字列は理由付きで拒否されるTest")]
        [TestCase("null", TestName = "壊れたworldVersion_nullは理由付きで拒否されるTest")]
        public void 壊れた版のセーブは理由付きで拒否されるTest(string worldVersionJson)
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = JToken.Parse(worldVersionJson);
            LogAssert.Expect(LogType.Error, new Regex("worldVersionが整数として読めません"));
            LogAssert.Expect(LogType.Error, new Regex("^セーブをロードできません"));

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsFalse(prepared.CanLoad);
            Assert.IsNotEmpty(prepared.BlockedReason);
            Assert.IsNull(prepared.SaveJsonText);
        }

        // マイグレーションが走らない現在版ではバックアップを作らない（毎回同じ原本を書き直さない）
        // No migration means no backup, so the same original is not rewritten on every boot
        [Test]
        public void 現在版のセーブではバックアップを作らないTest()
        {
            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);

            preparer.Prepare(SaveLoadPreparerTestFixture.BuildSaveJson().ToString());

            Assert.IsFalse(Directory.Exists(SaveArchiveDirectory.FromArchiveRoot(_archiveRoot).BackupRoot));
        }

        // 版1と記された既存セーブ（形式は既に版2相当）が、3項目を上書きされずに版2へ上がること
        // An existing save labelled version 1 whose shape is already version 2 must reach version 2 with the three fields untouched
        [Test]
        public void 版1のセーブは3項目を保ったまま版2へ上がり実ロードできるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;
            save["currentTick"] = 4321;

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            var migrated = JObject.Parse(prepared.SaveJsonText);
            Assert.AreEqual(WorldSaveAllInfoV1.CurrentVersion, migrated["worldVersion"].Value<int>());
            Assert.AreEqual(4321, migrated["currentTick"].Value<long>());
            Assert.AreEqual(save["randomState"].ToString(), migrated["randomState"].ToString());
            Assert.AreEqual(save["miningCooldowns"].ToString(), migrated["miningCooldowns"].ToString());

            var loader = SaveLoadPreparerTestFixture.CreateContainer().GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.DoesNotThrow(() => loader.Load(prepared.SaveJsonText));
        }

        // 退避した原本は原文一致で残り、2回目のロードでは書き換わらないこと
        // The archived original matches the source text byte for byte and a second load must not rewrite it
        [Test]
        public void 版1のセーブは原文のまま退避され2回目で上書きされないTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;
            var originalText = save.ToString();

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            preparer.Prepare(originalText);

            var backupPath = SaveArchiveDirectory.FromArchiveRoot(_archiveRoot).BackupSaveJsonPath(1);
            Assert.AreEqual(originalText, File.ReadAllText(backupPath));

            // 2回目は別内容を渡す。上書きされるならここで原本が失われる
            // The second run passes different content; if it overwrote, the original would be lost here
            var second = JObject.Parse(originalText);
            second["currentTick"] = 999;
            preparer.Prepare(second.ToString());

            Assert.AreEqual(originalText, File.ReadAllText(backupPath));
        }

        // 未来版のセーブで新規ワールド作成へ落ちないこと。落ちるとautosaveが原本を古い形式で上書きする
        // A future-version save must not fall through to world creation; it would let autosave overwrite the original in the old format
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

        // DIが実連鎖（V1→V2を1本積んだもの）を配線していること。積み忘れると版1が永久にロードできない
        // The container must wire the real chain with the V1-to-V2 step; forgetting it leaves version 1 unloadable forever
        [Test]
        public void DIから解決した連鎖が版1のセーブを版2へ上げるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;

            var result = SaveLoadPreparerTestFixture.CreateContainer().GetService<SaveMigrationChain>().Migrate(save);

            Assert.IsTrue(result.CanLoad, result.BlockedReason);
            Assert.IsTrue(result.Migrated);
            Assert.AreEqual(WorldSaveAllInfoV1.CurrentVersion, result.Save["worldVersion"].Value<int>());
        }
    }
}
