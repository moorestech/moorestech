using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Block.Interface;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Pruning;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game
{
    /// <summary>ロード前段のうち、マスタ欠損の除去・除去データの保管・件数の受け渡しを見る</summary>
    /// <summary>Covers the pre-load stage's pruning, pruned-data archiving and count hand-off</summary>
    public class SaveLoadPreparerTest
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

        // 欠損ブロックが例外なくロードできること
        // A save containing a removed block must pass the real load path without throwing
        [Test]
        public void 欠損ブロック入りのセーブが除去後に実ロードできるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["world"]).Add(SaveLoadPreparerTestFixture.MissingBlock(987660, 80));

            var (reportStore, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            // storeへの格納が通知側の唯一の入力なので、往復をここで固定する
            // The store is the notification side's only input, so the round trip is pinned here
            Assert.AreEqual(1, reportStore.Report.RemovedBlockCount);
            Assert.IsTrue(reportStore.Report.HasRemoval);

            var loader = SaveLoadPreparerTestFixture.CreateContainer().GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.DoesNotThrow(() => loader.Load(prepared.Save));

            // 除去ブロックIDがロード後残っていないこと
            // The removed block's instance id must be absent from the world after load
            Assert.IsFalse(ServerContext.WorldBlockDatastore.BlockMasterDictionary.ContainsKey(new BlockInstanceId(987660)));
        }

        // 除去データのファイル名はWindowsで使える基本形式。コロンが混じると保存そのものが失敗する
        // The pruned file name uses the basic form usable on Windows; a colon would make the write itself fail
        [Test]
        public void 除去があると除去データのファイルが1本できるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["world"]).Add(SaveLoadPreparerTestFixture.MissingBlock(987661, 81));

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            preparer.Prepare(save.ToString());

            var files = Directory.GetFiles(WorldDataDirectory.FromWorldRoot(_archiveRoot).SavePrunedDirectory, "*.json");
            Assert.AreEqual(1, files.Length);
            StringAssert.IsMatch(@"^\d{8}T\d{6}Z\.json$", Path.GetFileName(files[0]));

            var pruned = JObject.Parse(File.ReadAllText(files[0]));
            Assert.AreEqual(1, ((JArray)pruned["blocks"]).Count);
            Assert.AreEqual(SaveLoadPreparerTestFixture.MissingGuid, pruned["blocks"][0]["blockGuid"].Value<string>());
        }

        // 同秒2回ロードで除去データを上書きしないこと
        // Two loads within the same second must not overwrite each other's pruned data; every file is kept via the suffix
        [Test]
        public void 同じ秒の除去データは連番で全件残るTest()
        {
            var directory = WorldDataDirectory.FromWorldRoot(_archiveRoot);
            var writer = new SaveArchiveWriter(directory);
            var utcNow = new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc);
            var outcome = new MissingMasterPruneOutcome(new JObject(), Array.Empty<MissingMasterSectionPruneResult>());

            for (var i = 0; i < 3; i++) writer.WritePruned(outcome, utcNow);

            var prunedRoot = directory.SavePrunedDirectory;
            CollectionAssert.AreEquivalent(
                new[] { "20260913T083000Z.json", "20260913T083000Z-1.json", "20260913T083000Z-2.json" },
                Array.ConvertAll(Directory.GetFiles(prunedRoot, "*.json"), Path.GetFileName));
        }

        // 除去0件でファイル・レポートが生えないこと
        // The normal case: nothing removed leaves no file and no report
        [Test]
        public void 除去が無いと除去データのファイルは作られないTest()
        {
            var (reportStore, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);

            var prepared = preparer.Prepare(SaveLoadPreparerTestFixture.BuildSaveJson().ToString());

            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            Assert.IsFalse(reportStore.Report.HasRemoval);
            Assert.IsFalse(Directory.Exists(WorldDataDirectory.FromWorldRoot(_archiveRoot).SavePrunedDirectory));
        }

        // 補填の痕跡をロードで読み捨てると、次のautosaveで消えて捏造値と実値を区別できなくなる
        // Dropping the backfill trace on load would let the next autosave erase it, leaving placeholders indistinguishable from real values
        [Test]
        public void 補填した項目名はロードしてセーブし直しても残るTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["worldVersion"] = 1;
            save.Remove("currentTick");
            save.Remove("randomState");
            save.Remove("backfilledFields");

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());
            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);

            var serviceProvider = SaveLoadPreparerTestFixture.CreateContainer();
            (serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(prepared.Save);
            var resaved = JObject.Parse(serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());

            CollectionAssert.AreEqual(new[] { "currentTick", "randomState" }, resaved["backfilledFields"].ToObject<string[]>());
        }

        // 日付らしい文字列が既定のパースで日付型へ化けると、ロード・autosaveで綴りが書き換わる
        // If a date-like string became a date under the default parse, load and autosave would rewrite its spelling
        [Test]
        public void 日付らしい文字列は準備後も文字列のまま綴りが変わらないTest()
        {
            const string dateLikeText = "2026-09-13T08:30:00.1234567+09:00";
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["dateLikeProbe"] = dateLikeText;

            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            var probe = prepared.Save["dateLikeProbe"];
            Assert.AreEqual(JTokenType.String, probe.Type);
            Assert.AreEqual(dateLikeText, probe.Value<string>());
        }

        // JSONとして読めないセーブは生の例外で落とさず、原因つきで拒否する
        // A save unreadable as JSON is rejected with its cause instead of dying on a raw exception
        [Test]
        public void JSONとして読めないセーブは原因つきで拒否されるTest()
        {
            LogAssert.Expect(LogType.Error, new Regex("^セーブファイルのJSON解析に失敗しました"));
            LogAssert.Expect(LogType.Error, new Regex("^セーブファイルがJSONとして読めません"));
            var (_, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);

            var prepared = preparer.Prepare("{ not json");

            Assert.IsFalse(prepared.CanLoad);
            Assert.AreEqual(SaveLoadBlockedCause.UnreadableJson, prepared.BlockedCause);
        }

        // 読み取り面と書き込み面が同じ実体を指していること。別実体だと通知側が常に0件を読む
        // The read and write faces must be the same instance; otherwise the notification side always reads zero
        [Test]
        public void DIの除去件数は読み取り面と同じ実体であるTest()
        {
            var serviceProvider = SaveLoadPreparerTestFixture.CreateContainer();

            Assert.IsNotNull(serviceProvider.GetService<SaveLoadPreparer>());
            Assert.AreSame(serviceProvider.GetService<MissingMasterPruneReportStore>(), serviceProvider.GetService<IMissingMasterPruneReportLookup>());
        }
    }
}
