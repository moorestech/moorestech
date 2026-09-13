using System;
using System.IO;
using Game.Paths;
using Game.SaveLoad.Migration;
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
            var writer = new SaveArchiveWriter(SaveArchiveDirectory.FromArchiveRoot(root));

            writer.WriteBackup(1, "{\"first\":true}");
            writer.WriteBackup(1, "{\"second\":true}");

            var path = SaveArchiveDirectory.FromArchiveRoot(root).BackupSaveJsonPath(1);
            Assert.AreEqual("{\"first\":true}", File.ReadAllText(path));
            Directory.Delete(root, true);
        }

        // 同秒に2回除去が起きても片方が消えないことを見る
        // Two prunes in the same second must not overwrite each other
        [Test]
        public void 除去データは同秒でも連番で別ファイルになるTest()
        {
            var root = Path.Combine(Path.GetTempPath(), "moorestech-save-archive-" + Guid.NewGuid().ToString("N"));
            var directory = SaveArchiveDirectory.FromArchiveRoot(root);
            var writer = new SaveArchiveWriter(directory);
            var at = new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc);

            writer.WritePruned(JObject.Parse("{\"a\":1}"), at);
            writer.WritePruned(JObject.Parse("{\"a\":2}"), at);

            Assert.AreEqual(2, Directory.GetFiles(directory.PrunedRoot, "*.json").Length);
            Assert.IsTrue(File.Exists(directory.PrunedJsonPath(at, 0)));
            Assert.IsTrue(File.Exists(directory.PrunedJsonPath(at, 1)));
            Directory.Delete(root, true);
        }
    }
}
