using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Actions.Playtest;
using Client.WebUiHost.Game.Playtest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport
{
    // enum → ActionResult の写像を実ハンドラ越しに押さえる。ここが無いと Sent を Fail に取り違えても全テストが緑のままになる
    // Pins the enum → ActionResult mapping through the real handlers; without it, mistaking Sent for a failure keeps every test green
    public class PlaytestGateActionResultMappingTest
    {
        private const string WrittenDirectory = "/tmp/crash-bundle-double";

        private static ActionResult Respond(CrashReportGate gate, bool send)
        {
            return new CrashReportRespondActionHandler(gate).ExecuteAsync(new JObject { ["send"] = send, ["description"] = "説明" }).GetAwaiter().GetResult();
        }

        [Test]
        public void 送信と見送りは成功で返る()
        {
            var sent = Respond(new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), Unclean()), true);
            Assert.IsTrue(sent.Ok, "送信できた応答が失敗として返っている");

            var skipped = Respond(new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), Unclean()), false);
            Assert.IsTrue(skipped.Ok, "送らないを選んだ応答が失敗として返っている");
        }

        // 書き出し失敗はポーズメニュー経路と同じ理由コードで返す。成功へ丸めると送ったつもりのまま何も届かない
        // A failed write returns the pause-menu path's reason code; folding it into success leaves the user believing a lost report was sent
        [Test]
        public void 書き出し失敗はbundle_write_failedで返る()
        {
            var gate = new CrashReportGate(new RecordingCrashBundleWriter(null), Unclean());
            LogAssert.Expect(LogType.Error, "前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");

            var result = Respond(gate, true);
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("bundle_write_failed", result.Error);
        }

        [Test]
        public void 二度目の応答はalready_respondedで返る()
        {
            var gate = new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), Unclean());
            Respond(gate, true);

            var second = Respond(gate, true);
            Assert.IsFalse(second.Ok);
            Assert.AreEqual("already_responded", second.Error);
        }

        [Test]
        public void 二度目の了解はalready_acknowledgedで返る()
        {
            // 同意の既読フラグは本番と同じ場所に書かれるため、テストの前後で自分が作った分だけ消す
            // The consent flag lands in the production location, so the test removes only what it created
            var existed = PlaytestConsentFlag.IsAcknowledged();
            var gate = new PlaytestConsentGate(true);
            var handler = new AcknowledgePlaytestConsentActionHandler(gate);

            Assert.IsTrue(handler.ExecuteAsync(null).GetAwaiter().GetResult().Ok);
            var second = handler.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.IsFalse(second.Ok);
            Assert.AreEqual("already_acknowledged", second.Error);

            if (!existed && File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
        }

        private static PreviousSessionArtifacts Unclean()
        {
            return new PreviousSessionArtifacts { PreviousExitWasClean = false };
        }
    }
}
