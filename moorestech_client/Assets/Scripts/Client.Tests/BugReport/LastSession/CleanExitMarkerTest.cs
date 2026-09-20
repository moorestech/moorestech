using Client.Game.Common;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // 印のパスは CleanExitMarker の内側に閉じている。検証は公開の書き手・一覧・消費を通した結果だけで行う（C15）
    // The mark paths stay inside CleanExitMarker; verification goes only through the public writers, listing and consumption (C15)
    public class CleanExitMarkerTest
    {
        // 実プロセスと衝突しないpid。マーカーはマシン共通のBugReports/配下に置かれるため必ず後始末する
        // A pid that cannot collide with a real process; the marks live under the machine-wide BugReports/, so they are always cleaned up
        private const int TestProcessId = 999001;
        private const string OlderSessionName = "session_100";
        private const string CurrentSessionName = "session_200";

        // 消費は印を読んで段ごと消す。空になったpidのディレクトリも畳まれるので、後始末はこれで足りる
        // Consuming reads and removes a session's marks, folding the emptied pid directory too, so it is enough for cleanup
        [SetUp]
        [TearDown]
        public void RemoveMarkers()
        {
            GameShutdownEvent.ResetForNewSession();
            CleanExitMarker.ConsumeSessionMarks(TestProcessId, OlderSessionName);
            CleanExitMarker.ConsumeSessionMarks(TestProcessId, CurrentSessionName);
        }

        [Test]
        public void 正常終了の印があれば前回は正常終了と判定し印ごと消える()
        {
            CleanExitMarker.MarkSessionStarted(TestProcessId, OlderSessionName, new SessionOriginSnapshot("steam-1", BuildOriginReading.Editor(), new SessionSnapshotSource(false, "/tmp/world/snapshots")));
            CleanExitMarker.MarkExitIntent(TestProcessId, OlderSessionName);
            CleanExitMarker.MarkCleanExit(TestProcessId, OlderSessionName);

            var record = CleanExitMarker.ConsumeSessionMarks(TestProcessId, OlderSessionName);

            Assert.IsTrue(record.ExitedCleanly);
            Assert.IsFalse(record.ShutdownStalled);
            Assert.AreEqual("steam-1", record.Origin.SteamId, "開始時に書いた出所が読み戻せていない");
            Assert.AreEqual(BuildOriginKind.Editor, record.Origin.BuildOrigin.Kind);

            // 退避元も開始時の記録から読み戻す。次回起動の設定で代用しないための土台（D-C3）
            // The salvage source is read back from the start-time record too; this is the ground for never standing in with the next boot's settings (D-C3)
            Assert.IsFalse(record.Origin.SnapshotSource.IsRemoteConnection);
            Assert.AreEqual("/tmp/world/snapshots", record.Origin.SnapshotSource.WorldSnapshotDirectory);

            // 消えていれば一覧に出ず、もう一度消費しても正常終了の印も出所も読めない
            // Once removed it is not listed, and consuming again reads neither the clean mark nor the origin
            Assert.IsFalse(ContainsMarked(OlderSessionName), "消費した印が一覧に残っている");
            var again = CleanExitMarker.ConsumeSessionMarks(TestProcessId, OlderSessionName);
            Assert.IsFalse(again.ExitedCleanly, "消費した正常終了の印が残っている");
            Assert.IsNull(again.Origin, "消費した出所が残っている");
        }

        [Test]
        public void 開始の印だけが残っていれば前回は異常終了と判定する()
        {
            CleanExitMarker.MarkSessionStarted(TestProcessId, OlderSessionName, new SessionOriginSnapshot(null, BuildOriginReading.Editor(), null));

            Assert.IsTrue(ContainsMarked(OlderSessionName));
            var record = CleanExitMarker.ConsumeSessionMarks(TestProcessId, OlderSessionName);
            Assert.IsFalse(record.ExitedCleanly);
            Assert.IsFalse(record.ShutdownStalled, "終了の意思表明が無いのに終了処理中の停止と数えている");
        }

        // 終了の意思は出たのに書き出し完了の印が無い＝終了処理中に止まった。正常終了と読むとフリーズの資料を捨てる（F03）
        // An exit intent without the flush-finished mark means the session stopped during shutdown; reading it as clean would discard the freeze's evidence (F03)
        [Test]
        public void 終了の意思表明だけで書き出し完了の印が無ければ終了処理中の停止として数える()
        {
            CleanExitMarker.MarkSessionStarted(TestProcessId, OlderSessionName, new SessionOriginSnapshot(null, BuildOriginReading.Editor(), null));
            CleanExitMarker.MarkExitIntent(TestProcessId, OlderSessionName);

            var record = CleanExitMarker.ConsumeSessionMarks(TestProcessId, OlderSessionName);
            Assert.IsFalse(record.ExitedCleanly);
            Assert.IsTrue(record.ShutdownStalled);
        }

        // 生きているpidの印を消すと、そのEditorが本当に落ちても次回起動で検知できない（印が1つも残らないため）
        // Erasing a live pid's marks would leave its real crash undetectable at the next boot, since no mark survives it
        [Test]
        public void 生存している他pidの印は回収の対象にならず消えない()
        {
            CleanExitMarker.MarkSessionStarted(TestProcessId, OlderSessionName, new SessionOriginSnapshot(null, BuildOriginReading.Editor(), null));

            var scan = PreviousProcessScanner.Scan(0, CurrentSessionName, new RecordingProcessTakeover(), CleanExitMarker.MarkedSessions(), new[] { TestProcessId });

            Assert.Contains(TestProcessId, scan.SkippedLiveProcessIds);
            Assert.IsTrue(ContainsMarked(OlderSessionName), "回収の対象外にした生存pidの印が消えている");
        }

        // 同じpidでの再生し直し。旧セッションの印を「生きているから」と残すと、次のセッションの判定へ持ち越される（F05）
        // A same-pid replay: keeping the older session's marks "because the pid is alive" would carry them into the next session's verdict (F05)
        [Test]
        public void 自pidの今回以外のセッションは前回として数え今回のセッションは数えない()
        {
            CleanExitMarker.MarkSessionStarted(TestProcessId, OlderSessionName, new SessionOriginSnapshot(null, BuildOriginReading.Editor(), null));
            CleanExitMarker.MarkSessionStarted(TestProcessId, CurrentSessionName, new SessionOriginSnapshot(null, BuildOriginReading.Editor(), null));

            var scan = PreviousProcessScanner.Scan(TestProcessId, CurrentSessionName, new RecordingProcessTakeover(), CleanExitMarker.MarkedSessions(), new[] { TestProcessId });

            Assert.IsTrue(scan.Sessions.Exists(session => session.ProcessId == TestProcessId && session.SessionName == OlderSessionName), "同じpidの旧セッションが前回として数えられていない");
            Assert.IsFalse(scan.Sessions.Exists(session => session.SessionName == CurrentSessionName), "今回のセッションを前回として数えている");
            Assert.IsFalse(scan.SkippedLiveProcessIds.Contains(TestProcessId));
        }

        [Test]
        public void 意図的な終了以外は終了の印を書かない()
        {
            CleanExitMarkWriter.InstallAtStartup(TestProcessId, CurrentSessionName, new SessionSnapshotSource(false, "/tmp/world/snapshots"));

            // 初期化失敗でメインメニューへ戻る経路は、拾いたいクラッシュ側。ここで印を書くと録画が次回起動で捨てられる
            // The fold-up to the main menu after a failed initialization is the crash side; a mark here would discard the recording at the next boot
            GameShutdownEvent.FireGameShutdown(GameShutdownReason.InitializationFailed);
            var record = CleanExitMarker.ConsumeSessionMarks(TestProcessId, CurrentSessionName);
            Assert.IsFalse(record.ShutdownStalled, "意図的でない終了に終了の意思表明の印が書かれている");
            Assert.IsFalse(record.ExitedCleanly, "意図的でない終了に正常終了の印が書かれている");
        }

        // 意思表明の時点では正常終了の印を書かない。書き出しの途中で止まったセッションを正常終了と読まないため（F03）
        // The clean mark is not written at the intent, so a session that stops midway through the flush never reads as clean (F03)
        [Test]
        public void 正常終了の印は全参加者の書き出しが終わるまで書かれない()
        {
            CleanExitMarkWriter.InstallAtStartup(TestProcessId, CurrentSessionName, new SessionSnapshotSource(false, "/tmp/world/snapshots"));
            var participant = new ControllableShutdownParticipant();
            GameShutdownEvent.RegisterParticipant(participant);

            // 書き出し中に消費すると「意思表明あり・完了なし」＝終了処理中の停止として読める
            // Consuming during the flush reads "intent present, completion absent", i.e. a stop during shutdown
            var shutdown = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            var duringFlush = CleanExitMarker.ConsumeSessionMarks(TestProcessId, CurrentSessionName);
            Assert.IsTrue(duringFlush.ShutdownStalled, "終了の意思表明の印が書かれていない");
            Assert.IsFalse(duringFlush.ExitedCleanly, "書き出しの完了前に正常終了の印が書かれている");

            participant.Complete(ShutdownFlushResult.Flushed);
            shutdown.GetAwaiter().GetResult();
            Assert.IsTrue(CleanExitMarker.ConsumeSessionMarks(TestProcessId, CurrentSessionName).ExitedCleanly, "書き出し完了後に正常終了の印が書かれていない");
        }

        // EditorのPlay停止や破棄は書き出し完了を待てない。完了の印を待つと、正常に止めたのに次回が毎回クラッシュ扱いになる
        // An Editor Play stop or teardown cannot await the flush; waiting for completion would make every normal stop read as a crash next time
        [Test]
        public void 待てない終了は意思表明の時点で正常終了の印を書く()
        {
            CleanExitMarkWriter.InstallAtStartup(TestProcessId, CurrentSessionName, new SessionSnapshotSource(false, "/tmp/world/snapshots"));
            GameShutdownEvent.RegisterParticipant(new ControllableShutdownParticipant());

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, "書き出し完了を待てない終了のため、意思表明の時点で正常終了として記録します（終了処理中の停止はこの経路では検知できません）");
            GameShutdownEvent.FireGameShutdown(GameShutdownReason.UnawaitableExit);

            Assert.IsTrue(CleanExitMarker.ConsumeSessionMarks(TestProcessId, CurrentSessionName).ExitedCleanly, "待てない終了で正常終了の印が書かれていない");
        }

        private static bool ContainsMarked(string sessionName)
        {
            foreach (var marked in CleanExitMarker.MarkedSessions())
                if (marked.ProcessId == TestProcessId && marked.SessionName == sessionName) return true;
            return false;
        }

        // 完了タイミングをテストが握る参加者。PlayerLoop無しのEditModeでも決定的に進められる
        // A participant whose completion the test drives, so EditMode without a PlayerLoop stays deterministic
        private sealed class ControllableShutdownParticipant : IGameShutdownParticipant
        {
            private readonly UniTaskCompletionSource<ShutdownFlushResult> _flushCompletionSource = new();

            public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                return _flushCompletionSource.Task;
            }

            public void Complete(ShutdownFlushResult flushResult)
            {
                _flushCompletionSource.TrySetResult(flushResult);
            }
        }
    }
}
