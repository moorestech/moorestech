using System;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest
{
    // 書き出した後に current/ が復活しないこと（＝次回起動で偽の記録が出ないこと）を固定する
    // Fixes that current/ never comes back after the record is written, so no bogus record appears at the next boot
    public class ProgressSessionWriterTest
    {
        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressRecordFiles.ClearCurrent();
        }

        private static ProgressRecordHeader CreateHeader()
        {
            return new ProgressRecordHeader { SessionStart = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") };
        }

        [Test]
        public void 書き出し後のヘッダ更新はcurrentを復活させない()
        {
            var writer = new ProgressSessionWriter();
            writer.WriteHeader(CreateHeader());
            writer.Append(ProgressEventEntry.Create(DateTime.UtcNow, 1, ProgressEventType.BlockPlaced, new JObject()));
            var bundle = writer.Close(ProgressSessionRecovery.QuitEndReason, DateTime.UtcNow);
            Assert.IsNotNull(bundle);

            LogAssert.Expect(LogType.Warning, new Regex("ヘッダを更新しません"));
            writer.WriteHeader(CreateHeader());

            Assert.IsFalse(File.Exists(ProgressRecordPaths.CurrentHeaderPath));
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void 書き出し後の追記はcurrentを復活させない()
        {
            var writer = new ProgressSessionWriter();
            writer.WriteHeader(CreateHeader());
            var bundle = writer.Close(ProgressSessionRecovery.QuitEndReason, DateTime.UtcNow);

            LogAssert.Expect(LogType.Warning, new Regex("追記しません"));
            writer.Append(ProgressEventEntry.Create(DateTime.UtcNow, 2, ProgressEventType.BlockPlaced, new JObject()));

            Assert.IsFalse(File.Exists(ProgressRecordPaths.CurrentEventsPath));
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());
            Directory.Delete(bundle, true);
        }
    }
}
