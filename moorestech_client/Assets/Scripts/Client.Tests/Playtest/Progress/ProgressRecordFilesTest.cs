using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressRecordFilesTest
    {
        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressRecordFiles.ClearCurrent();
        }

        [Test]
        public void ヘッダとイベントを書いて閉じるとoutboxに箱が出る()
        {
            var start = DateTime.UtcNow;
            ProgressRecordFiles.WriteHeader(new ProgressRecordHeader
            {
                SessionStart = start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                WorldCreatedAt = "2026-09-10T09:00:00Z",
                SteamId = "",
                TotalPlaySecondsAtStart = 0,
            });
            Assert.IsTrue(ProgressRecordFiles.HasCurrentSession());

            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(start, 1, ProgressEventType.BlockPlaced, new JObject()));
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(start, 2, ProgressEventType.BlockPlaced, new JObject()));
            Assert.AreEqual(2, ProgressRecordFiles.ReadEvents().Count);

            var bundle = ProgressRecordFiles.CloseCurrentInto("crash-recovered", start.AddSeconds(30));

            Assert.IsTrue(File.Exists(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.IsTrue(File.Exists(Path.Combine(bundle, "READY")));
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual("crash-recovered", (string)record["endReason"]);
            Assert.AreEqual(2, (int)record["placedBlockCount"]);
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void ヘッダが無いのに閉じようとしたらnullを返す()
        {
            Assert.IsNull(ProgressRecordFiles.CloseCurrentInto("quit", DateTime.UtcNow));
        }

        [Test]
        public void 壊れたイベント行は飛ばして残りを読む()
        {
            ProgressRecordFiles.WriteHeader(new ProgressRecordHeader { SessionStart = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") });
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow, 1, ProgressEventType.BlockPlaced, new JObject()));
            File.AppendAllText(ProgressRecordPaths.CurrentEventsPath, "{ broken\n");
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow, 2, ProgressEventType.BlockPlaced, new JObject()));

            var events = ProgressRecordFiles.ReadEvents();
            Assert.AreEqual(2, events.Count);
        }
    }
}
