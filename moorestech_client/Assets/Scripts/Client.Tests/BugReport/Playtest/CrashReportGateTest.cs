using System;
using Client.Game.InGame.BugReport.LastSession;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport
{
    // 応答が1回だけ効き、その1回で待機が解けることを押さえる（ADR 0040 の言語選択ゲートと同じ契約）
    // Pins that exactly one answer takes effect and releases the wait (the same contract as the ADR 0040 language gate)
    public class CrashReportGateTest
    {
        private const string WrittenDirectory = "/tmp/crash-bundle-double";

        private static PreviousSessionArtifacts Unclean()
        {
            return new PreviousSessionArtifacts { PreviousExitWasClean = false };
        }

        [Test]
        public void 前回が正常終了ならゲートは待たない()
        {
            var gate = new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), new PreviousSessionArtifacts { PreviousExitWasClean = true });
            Assert.IsFalse(gate.IsWaitingSelection());
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        [Test]
        public void 送らないを選ぶと箱を作らず待機が解ける()
        {
            var writer = new RecordingCrashBundleWriter(WrittenDirectory);
            var gate = new CrashReportGate(writer, Unclean());

            Assert.AreEqual(CrashReportResponseResult.Skipped, gate.RespondAsync(false, "").GetAwaiter().GetResult());
            Assert.IsFalse(gate.IsWaitingSelection());
            CollectionAssert.IsEmpty(writer.Descriptions);
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        [Test]
        public void 送るを選ぶと説明文付きで箱を書かせ二度目の応答は弾かれる()
        {
            var writer = new RecordingCrashBundleWriter(WrittenDirectory);
            var gate = new CrashReportGate(writer, Unclean());

            Assert.AreEqual(CrashReportResponseResult.Sent, gate.RespondAsync(true, "落ちた").GetAwaiter().GetResult());
            Assert.AreEqual(CrashReportResponseResult.AlreadyResponded, gate.RespondAsync(true, "二重").GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { "落ちた" }, writer.Descriptions);
        }

        // 待機しないゲートへの応答も「応答済み」で弾く。正常終了後に遅れて届いたクリックで箱が出来ないこと
        // An answer to a non-waiting gate is rejected too, so a late click after a clean exit cannot create a box
        [Test]
        public void 待機していないゲートへの応答は弾かれる()
        {
            var writer = new RecordingCrashBundleWriter(WrittenDirectory);
            var gate = new CrashReportGate(writer, new PreviousSessionArtifacts { PreviousExitWasClean = true });

            Assert.AreEqual(CrashReportResponseResult.AlreadyResponded, gate.RespondAsync(true, "遅れて届いた").GetAwaiter().GetResult());
            CollectionAssert.IsEmpty(writer.Descriptions);
        }

        // 箱を書けなかったら待機へ戻す。閉じてしまうと唯一の証跡が無音で消え、送り直す手段も残らない
        // A failed write returns the gate to waiting; closing would drop the only evidence silently with no way to resend
        [Test]
        public void 箱を書けなかったら待機へ戻り送り直せる()
        {
            var failing = new RecordingCrashBundleWriter(null);
            var gate = new CrashReportGate(failing, Unclean());
            LogAssert.Expect(LogType.Error, "前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");

            Assert.AreEqual(CrashReportResponseResult.WriteFailed, gate.RespondAsync(true, "書けない").GetAwaiter().GetResult());
            Assert.IsTrue(gate.IsWaitingSelection());
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
            var gate = new CrashReportGate(new ThrowingCrashBundleWriter(), Unclean());
            LogAssert.Expect(LogType.Error, "前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");

            Assert.Throws<NotSupportedException>(() => gate.RespondAsync(true, "書けない").GetAwaiter().GetResult());

            Assert.IsTrue(gate.IsWaitingSelection());
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
