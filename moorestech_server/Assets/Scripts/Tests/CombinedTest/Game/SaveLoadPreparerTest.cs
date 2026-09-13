using System;
using System.IO;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Pruning;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

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

        // 除去された欠損ブロックを含むセーブが、実ロード経路で例外を出さずに通ること
        // A save containing a removed block must pass the real load path without throwing
        [Test]
        public void 欠損ブロック入りのセーブが除去後に実ロードできるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["world"]).Add(SaveLoadPreparerTestFixture.MissingBlock(987660, 80));

            var (reportStore, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);
            var prepared = preparer.Prepare(save.ToString());

            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            Assert.AreEqual(1, prepared.Report.RemovedBlockCount);
            // storeへの格納が通知側の唯一の入力なので、往復をここで固定する
            // The store is the notification side's only input, so the round trip is pinned here
            Assert.AreEqual(1, reportStore.Report.RemovedBlockCount);
            Assert.IsTrue(reportStore.Report.HasRemoval);

            var loader = SaveLoadPreparerTestFixture.CreateContainer().GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            Assert.DoesNotThrow(() => loader.Load(prepared.SaveJsonText));
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

            var files = Directory.GetFiles(SaveArchiveDirectory.FromArchiveRoot(_archiveRoot).PrunedRoot, "*.json");
            Assert.AreEqual(1, files.Length);
            StringAssert.IsMatch(@"^\d{8}T\d{6}Z\.json$", Path.GetFileName(files[0]));

            var pruned = JObject.Parse(File.ReadAllText(files[0]));
            Assert.AreEqual(1, ((JArray)pruned["blocks"]).Count);
            Assert.AreEqual(SaveLoadPreparerTestFixture.MissingGuid, pruned["blocks"][0]["blockGuid"].Value<string>());
        }

        // 同秒に2回ロードしても後の除去データが前を消さないこと（連番で全件残す）
        // Two loads within the same second must not overwrite each other's pruned data; every file is kept via the suffix
        [Test]
        public void 同じ秒の除去データは連番で全件残るTest()
        {
            var writer = new SaveArchiveWriter(SaveArchiveDirectory.FromArchiveRoot(_archiveRoot));
            var utcNow = new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc);

            for (var i = 0; i < 3; i++) writer.WritePruned(new JObject { ["blocks"] = new JArray() }, utcNow);

            var prunedRoot = SaveArchiveDirectory.FromArchiveRoot(_archiveRoot).PrunedRoot;
            CollectionAssert.AreEquivalent(
                new[] { "20260913T083000Z.json", "20260913T083000Z-1.json", "20260913T083000Z-2.json" },
                Array.ConvertAll(Directory.GetFiles(prunedRoot, "*.json"), Path.GetFileName));
        }

        // 除去0件（本番の常態）でファイルもレポートも生えないこと
        // The normal case: nothing removed leaves no file and no report
        [Test]
        public void 除去が無いと除去データのファイルは作られないTest()
        {
            var (reportStore, preparer) = SaveLoadPreparerTestFixture.CreatePreparer(_archiveRoot);

            var prepared = preparer.Prepare(SaveLoadPreparerTestFixture.BuildSaveJson().ToString());

            Assert.IsTrue(prepared.CanLoad, prepared.BlockedReason);
            Assert.IsFalse(prepared.Report.HasRemoval);
            Assert.IsFalse(reportStore.Report.HasRemoval);
            Assert.IsFalse(Directory.Exists(SaveArchiveDirectory.FromArchiveRoot(_archiveRoot).PrunedRoot));
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
