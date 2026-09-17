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

        // 非整数版は型例外でなく理由付き拒否で抜けること
        // A save whose version is not an integer must exit through a reasoned rejection, not a bare cast exception
        [TestCase("\"abc\"", TestName = "壊れたworldVersion_文字列は理由付きで拒否されるTest")]
        [TestCase("null", TestName = "壊れたworldVersion_nullは理由付きで拒否されるTest")]
        // int範囲外の整数は型としては整数なので、範囲判定が無いとOverflowExceptionで無ログに落ちる
        // An out-of-range integer is still typed as an integer, so without a range check it dies on OverflowException with no log
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

        // 版が上がらないロードでも除去結果がautosaveで原本を上書きするので、現在版でも原本を退避する
        // Even without a migration autosave would overwrite the original with the pruned result, so the current version is archived too
        [Test]
        public void 現在版のセーブも欠損ブロックの除去前に原本を退避するTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["world"]).Add(SaveLoadPreparerTestFixture.MissingBlock(987663, 83));
            var originalText = save.ToString();

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            preparer.Prepare(originalText);

            // 退避版が固定値へ退行していないことも、版1側に作られていないことで一緒に見る
            // Checking that nothing lands under version 1 also catches a regression to a hard-coded version
            var archive = WorldDataDirectory.FromWorldRoot(_archiveRoot);
            Assert.AreEqual(originalText, File.ReadAllText(archive.BackupSaveJsonPath(WorldSaveAllInfo.CurrentVersion)));
            Assert.IsFalse(File.Exists(archive.BackupSaveJsonPath(1)), "現在版のセーブが版1として退避されています");
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
            var migrated = prepared.Save;
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, migrated["worldVersion"].Value<int>());
            Assert.AreEqual(4321, migrated["currentTick"].Value<long>());
            Assert.AreEqual(save["randomState"].ToString(), migrated["randomState"].ToString());
            Assert.AreEqual(save["miningCooldowns"].ToString(), migrated["miningCooldowns"].ToString());

            var loader = SaveLoadPreparerTestFixture.CreateContainer().GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.DoesNotThrow(() => loader.Load(prepared.Save));
        }

        // 退避原本は原文一致で不変であること
        // The archived original matches the source text byte for byte and a second load must not rewrite it
        [Test]
        public void 版1のセーブは原文のまま退避され2回目で上書きされないTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;
            var originalText = save.ToString();

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            preparer.Prepare(originalText);

            var backupPath = WorldDataDirectory.FromWorldRoot(_archiveRoot).BackupSaveJsonPath(1);
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

        // LoadOrInitializeという本番の結線そのもので検証する。preparer.Prepare→loader.Loadの手組みでは
        // 「Prepareの出力ではなく生JSONをLoadに渡す」という配線側の退行を検出できない
        // Verified through the real LoadOrInitialize wiring; a hand-wired preparer.Prepare→loader.Load in the test
        // would miss a wiring regression where Load is fed the raw JSON instead of Prepare's output
        [Test]
        public void 版1のセーブはLoadOrInitializeで実際にロードできるTest()
        {
            var saveJsonFilePath = Path.Combine(_archiveRoot, "save.json");
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;
            // 版1が本来欠く3項目を落とす。生JSONを直接LoadすればcurrentTickが無く即例外になるため、
            // 通ること自体がPrepareの出力（変換で補填済み）が使われている証拠になる
            // Drop the three fields a real version-1 save lacks; the raw JSON would throw immediately on Load
            // (missing currentTick), so success here proves Prepare's backfilled output is what gets loaded
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
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, result.Save["worldVersion"].Value<int>());
        }
    }
}
