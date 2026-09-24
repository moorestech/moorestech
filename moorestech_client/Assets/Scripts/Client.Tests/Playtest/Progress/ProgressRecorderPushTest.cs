using System.IO;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.Playtest.Progress.Storage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest
{
    // 記録を開始していない起動（常時記録オフのテスト・DSL・調査用）で、プッシュが current/ を作らないことを固定する
    // Fixes that a push never conjures current/ on a boot that started no recording (a capture-off test, DSL or investigation boot)
    // 開始済みセッションでのプッシュ→record.json は PlaytestReportAndProgressTest が常時記録を有効にした実起動で押さえている
    // A push reaching record.json in a started session is pinned by PlaytestReportAndProgressTest on a real boot with capture enabled
    public class ProgressRecorderPushTest
    {
        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressTestSession.Clear();
        }

        // プッシュ経路は ctor で受けた依存を一切使わない。Initialize を呼ばない限り購読も張られない
        // The push path touches none of the ctor dependencies, and no subscription is made unless Initialize runs
        private static ProgressRecorder CreateRecorderForPushOnly()
        {
            return new ProgressRecorder(null, null, new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
        }

        // ヘッダの無い events.jsonl が残ると、次回起動が「一度も遊んでいないセッション」を1件 outbox へ出す
        // A headerless events.jsonl left behind makes the next boot ship one record for a session nobody ever played
        [Test]
        public void 記録を開始していなければプッシュはcurrentを作らない()
        {
            var recorder = CreateRecorderForPushOnly();

            LogAssert.Expect(LogType.Log, new Regex("進行記録を開始していないためプッシュを記録しません"));
            recorder.RecordReportSent(PlaytestReportKind.Bug);

            Assert.IsFalse(File.Exists(ProgressRecordPaths.EventsPathIn(ProgressTestSession.Directory)));
            Assert.IsFalse(ProgressTestSession.HasCurrentSession());
        }

        // 書き出す中身が無いのに Flushed を返すと、記録が1件も出ていない起動が成功として流れる
        // Returning Flushed with nothing to write would let a boot that produced no record pass as a success
        [Test]
        public void 何も書かずに終了するとNothingFlushedを返す()
        {
            var recorder = CreateRecorderForPushOnly();

            LogAssert.Expect(LogType.Warning, new Regex("書き出す進行記録がありませんでした"));
            Assert.AreEqual(ShutdownFlushResult.NothingFlushed, recorder.FlushOnShutdownAsync().GetAwaiter().GetResult());
        }
    }
}
