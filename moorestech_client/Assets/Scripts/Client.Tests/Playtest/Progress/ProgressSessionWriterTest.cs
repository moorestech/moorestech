using System;
using System.IO;
using System.Linq;
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
            ProgressTestSession.Clear();
        }

        private static ProgressRecordHeader CreateHeader()
        {
            return new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow) };
        }

        [Test]
        public void 書き出し後のヘッダ更新はcurrentを復活させない()
        {
            var writer = new ProgressSessionWriter();
            writer.WriteHeader(CreateHeader());
            writer.Append(ProgressEvents.BlockPlaced(DateTime.UtcNow, 1, 1));
            var bundle = writer.Close(ProgressEndReason.Quit, DateTime.UtcNow).BundleDirectory;
            Assert.IsNotNull(bundle);

            LogAssert.Expect(LogType.Warning, new Regex("ヘッダを更新しません"));
            writer.WriteHeader(CreateHeader());

            Assert.IsFalse(File.Exists(ProgressRecordPaths.HeaderPathIn(ProgressTestSession.Directory)));
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void 書き出し後の追記はcurrentを復活させない()
        {
            var writer = new ProgressSessionWriter();
            writer.WriteHeader(CreateHeader());
            var bundle = writer.Close(ProgressEndReason.Quit, DateTime.UtcNow).BundleDirectory;

            LogAssert.Expect(LogType.Warning, new Regex("追記しません"));
            writer.Append(ProgressEvents.BlockPlaced(DateTime.UtcNow, 2, 1));

            Assert.IsFalse(File.Exists(ProgressRecordPaths.EventsPathIn(ProgressTestSession.Directory)));
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        // 応答が終了に間に合わなかったケースだけが無音で 空/0 に確定していた。欠損として理由が残ることを固定する
        // Only the case where the response missed the shutdown settled silently into empty/zero; this pins that the reason is recorded
        [Test]
        public void プレイ時間が届かないまま閉じると欠損として残る()
        {
            var writer = new ProgressSessionWriter();
            writer.WriteHeader(CreateHeader());
            LogAssert.Expect(LogType.Warning, new Regex("worldPlayTime"));
            var bundle = writer.Close(ProgressEndReason.Quit, DateTime.UtcNow).BundleDirectory;

            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            var items = ((JArray)record["missing"]).Select(item => (string)item["item"]).ToList();
            CollectionAssert.Contains(items, "worldPlayTime");
            Directory.Delete(bundle, true);
        }

        // 届いた時刻を起点に累計へ足す。セッション開始起点だと応答待ちとロードのぶんが二重計上される
        // The total counts from the moment the value arrived; counting from the session start would double count the wait and the load
        [Test]
        public void 累計プレイ時間は取得時刻からの経過だけを足す()
        {
            var writer = new ProgressSessionWriter();
            var sessionStart = DateTime.UtcNow.AddSeconds(-60);
            writer.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(sessionStart) });
            var capturedAt = sessionStart.AddSeconds(40);
            writer.UpdateWorldPlayTime(ProgressWorldPlayTime.Received("2026-09-10T09:00:00Z", 100, capturedAt));

            var bundle = writer.Close(ProgressEndReason.Quit, capturedAt.AddSeconds(20)).BundleDirectory;

            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(120d, (double)record["totalPlaySeconds"], 1d, "取得時刻からの20秒だけが足されていない");
            Assert.AreEqual(60d, (double)record["playSeconds"], 1d);
            Directory.Delete(bundle, true);
        }
    }
}
