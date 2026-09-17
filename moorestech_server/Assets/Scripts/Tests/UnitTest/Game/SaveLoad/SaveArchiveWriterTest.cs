using System;
using System.IO;
using Game.Paths;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Pruning;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    /// <summary>退避の書き込み。原本を壊さないことと、同秒の除去データが消えないことを見る</summary>
    /// <summary>Archive writing: the original is never destroyed and same-second pruned data is never lost</summary>
    public class SaveArchiveWriterTest
    {
        [Test]
        public void バックアップは既存の原本を上書きしないTest()
        {
            var root = Path.Combine(Path.GetTempPath(), "moorestech-save-archive-" + Guid.NewGuid().ToString("N"));
            var directory = WorldDataDirectory.FromWorldRoot(root);
            var writer = new SaveArchiveWriter(directory);

            writer.WriteBackup(1, "{\"first\":true}");
            writer.WriteBackup(1, "{\"second\":true}");

            Assert.AreEqual("{\"first\":true}", File.ReadAllText(directory.BackupSaveJsonPath(1)));
            Directory.Delete(root, true);
        }

        // 同秒に2回除去が起きても片方が消えないことを見る
        // Two prunes in the same second must not overwrite each other
        [Test]
        public void 除去データは同秒でも連番で別ファイルになるTest()
        {
            var root = Path.Combine(Path.GetTempPath(), "moorestech-save-archive-" + Guid.NewGuid().ToString("N"));
            var directory = WorldDataDirectory.FromWorldRoot(root);
            var writer = new SaveArchiveWriter(directory);
            var at = new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc);

            writer.WritePruned(EmptyOutcome(), at);
            writer.WritePruned(EmptyOutcome(), at);

            Assert.AreEqual(2, Directory.GetFiles(directory.SavePrunedDirectory, "*.json").Length);
            Assert.IsTrue(File.Exists(directory.PrunedJsonPath(at, 0)));
            Assert.IsTrue(File.Exists(directory.PrunedJsonPath(at, 1)));

            // ファイル名とprunedAtが同じ時刻から綴られていること。JObject.Parseは日付を解釈し直すので原文で見る
            // The file name and prunedAt must share one timestamp; JObject.Parse reinterprets dates, so the raw text is checked
            StringAssert.Contains("\"prunedAt\": \"2026-09-13T08:30:00Z\"", File.ReadAllText(directory.PrunedJsonPath(at, 0)));
            Directory.Delete(root, true);
        }

        private static MissingMasterPruneOutcome EmptyOutcome()
        {
            return new MissingMasterPruneOutcome(new JObject(), Array.Empty<MissingMasterSectionPruneResult>());
        }
    }
}
