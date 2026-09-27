using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Block.Interface;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game
{
    /// <summary>本番ロード経路が版変換とマスタ欠損除去を通ることを検証する</summary>
    /// <summary>Verifies that the production loader applies migration and missing-master pruning</summary>
    public class SaveLoadPreparerLoaderWiringTest
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

        // 本番のLoadOrInitializeを通し、変換前の生JSONをLoadへ渡す配線の退行を検出する
        // Exercise production LoadOrInitialize to detect regressions that pass raw JSON directly to Load
        [Test]
        public void 版1のセーブはLoadOrInitializeで実際にロードできるTest()
        {
            var saveJsonFilePath = Path.Combine(_archiveRoot, "save.json");
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;
            save.Remove("players");
            // 版1が欠く項目を落とし、補填後のデータがロードへ渡ることを確認する
            // Remove fields absent in version 1 so loading requires the preparer's backfilled output
            save.Remove("currentTick");
            save.Remove("randomState");
            save.Remove("miningCooldowns");
            Directory.CreateDirectory(_archiveRoot);
            File.WriteAllText(saveJsonFilePath, save.ToString());

            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, saveJsonFilePath),
            };
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);

            Assert.DoesNotThrow(() => serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize());
        }

        // 同じくLoadOrInitializeの本番結線で検証する。マスタに無いblockGuidは生JSONのままLoadすれば解決時に例外になる
        // Also verified through the real LoadOrInitialize wiring; a blockGuid absent from the master throws on Load if the raw JSON is used
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
    }
}
