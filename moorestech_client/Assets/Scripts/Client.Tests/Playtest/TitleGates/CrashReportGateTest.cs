using System;
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.BugReport;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest.TitleGates
{
    // 応答が1回だけ効き、その1回で待機が解けることを押さえる（ADR 0040 の言語選択ゲートと同じ契約）
    // Pins that exactly one answer takes effect and releases the wait (the same contract as the ADR 0040 language gate)
    public class CrashReportGateTest
    {
        private const string WrittenDirectory = "/tmp/crash-bundle-double";

        [Test]
        public void 前回が正常終了ならゲートは待たない()
        {
            var gate = new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Clean());
            Assert.IsFalse(gate.IsWaitingResponse);
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        // 無人起動の閉じたゲートは退避結果を借りずに閉じている。遅れて届いた応答で箱が出来ないこと（F13）
        // An unattended boot's closed gate is closed without borrowing any salvage result, and a late answer cannot create a box (F13)
        [Test]
        public void 閉じたゲートは待たず応答も弾く()
        {
            var gate = CrashReportGate.Closed();

            Assert.IsFalse(gate.IsWaitingResponse);
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
            Assert.AreEqual(CrashReportResponseResult.AlreadyResponded, gate.RespondAsync(true, "遅れて届いた").GetAwaiter().GetResult());
        }

        [Test]
        public void 送らないを選ぶと箱を作らず待機が解ける()
        {
            var writer = new RecordingCrashBundleWriter(WrittenDirectory);
            var gate = new CrashReportGate(writer, TestPreviousSessionArtifacts.Unclean());

            Assert.AreEqual(CrashReportResponseResult.Skipped, gate.RespondAsync(false, "").GetAwaiter().GetResult());
            Assert.IsFalse(gate.IsWaitingResponse);
            CollectionAssert.IsEmpty(writer.Descriptions);
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        [Test]
        public void 送るを選ぶと説明文付きで箱を書かせ二度目の応答は弾かれる()
        {
            var writer = new RecordingCrashBundleWriter(WrittenDirectory);
            var gate = new CrashReportGate(writer, TestPreviousSessionArtifacts.Unclean());

            Assert.AreEqual(CrashReportResponseResult.Sent, gate.RespondAsync(true, "落ちた").GetAwaiter().GetResult());
            Assert.AreEqual(CrashReportResponseResult.AlreadyResponded, gate.RespondAsync(true, "二重").GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { "落ちた" }, writer.Descriptions);
        }

        // 答えた起動だけが未応答の印を消す。書けなかった応答は答えたことにならず、印は残って次回も聞き直せる（F04）
        // Only a boot that answered clears the pending mark; a failed write is not an answer, so the mark stays and the next boot asks again (F04)
        [Test]
        public void 応答すると未応答の印が消え書けなかった応答では残る()
        {
            var lastSession = Path.Combine(Path.GetTempPath(), $"moorestech-gate-{Guid.NewGuid():N}");
            try
            {
                PendingCrashReportMark.MarkPending(lastSession);
                var failing = new CrashReportGate(new RecordingCrashBundleWriter(null), TestPreviousSessionArtifacts.UncleanIn(lastSession));
                LogAssert.Expect(LogType.Error, "前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");
                failing.RespondAsync(true, "書けない").GetAwaiter().GetResult();
                Assert.IsTrue(PendingCrashReportMark.IsPending(lastSession), "書けなかったのに未応答の印が消えている");

                Assert.AreEqual(CrashReportResponseResult.Skipped, failing.RespondAsync(false, "").GetAwaiter().GetResult());
                Assert.IsFalse(PendingCrashReportMark.IsPending(lastSession), "送らないと答えたのに未応答の印が残っている");
            }
            finally
            {
                if (Directory.Exists(lastSession)) Directory.Delete(lastSession, true);
            }
        }

        // 待機しないゲートへの応答も「応答済み」で弾く。正常終了後に遅れて届いたクリックで箱が出来ないこと
        // An answer to a non-waiting gate is rejected too, so a late click after a clean exit cannot create a box
        [Test]
        public void 待機していないゲートへの応答は弾かれる()
        {
            var writer = new RecordingCrashBundleWriter(WrittenDirectory);
            var gate = new CrashReportGate(writer, TestPreviousSessionArtifacts.Clean());

            Assert.AreEqual(CrashReportResponseResult.AlreadyResponded, gate.RespondAsync(true, "遅れて届いた").GetAwaiter().GetResult());
            CollectionAssert.IsEmpty(writer.Descriptions);
        }

        // 箱を書けなかったら待機へ戻す。閉じてしまうと唯一の証跡が無音で消え、送り直す手段も残らない
        // A failed write returns the gate to waiting; closing would drop the only evidence silently with no way to resend
        [Test]
        public void 箱を書けなかったら待機へ戻り送り直せる()
        {
            var failing = new RecordingCrashBundleWriter(null);
            var gate = new CrashReportGate(failing, TestPreviousSessionArtifacts.Unclean());
            LogAssert.Expect(LogType.Error, "前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");

            Assert.AreEqual(CrashReportResponseResult.WriteFailed, gate.RespondAsync(true, "書けない").GetAwaiter().GetResult());
            Assert.IsTrue(gate.IsWaitingResponse);
            Assert.IsFalse(gate.WaitForResponseAsync().Status.IsCompleted());

            // 「送らない」は常に押せるので、書き出しが直らなくても起動は先へ進める
            // "Do not send" stays available, so the boot proceeds even when the write never recovers
            Assert.AreEqual(CrashReportResponseResult.Skipped, gate.RespondAsync(false, "").GetAwaiter().GetResult());
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        // 書き出しがディスク以外の例外で抜けても待機へ戻る。ここで待機を落とすと再送も弾かれ証跡が消える
        // A non-disk exception also returns the gate to waiting; dropping the wait here would reject the resend and lose the evidence
        [Test]
        public void 書き出しが例外で抜けても待機へ戻る()
        {
            var gate = new CrashReportGate(new ThrowingCrashBundleWriter(), TestPreviousSessionArtifacts.Unclean());
            LogAssert.Expect(LogType.Error, "前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");

            Assert.Throws<NotSupportedException>(() => gate.RespondAsync(true, "書けない").GetAwaiter().GetResult());

            Assert.IsTrue(gate.IsWaitingResponse);
            Assert.IsFalse(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        // テスト専用: ディスク由来ではない失敗（CrashBundleWriterが握らない種類）を確実に再現する
        // Test-only: reproduces a non-disk failure, the kind CrashBundleWriter does not catch
        private sealed class ThrowingCrashBundleWriter : ICrashBundleWriter
        {
            public UniTask<string> WriteAsync(PreviousSessionArtifacts artifacts, string description)
            {
                throw new NotSupportedException("書き出しに失敗した");
            }
        }
    }
}
