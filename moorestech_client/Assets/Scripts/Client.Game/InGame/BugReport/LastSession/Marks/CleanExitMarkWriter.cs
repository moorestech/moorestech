using System;
using Client.Game.Common;
using Client.Game.InGame.BugReport.Playtest;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 印の消費と同じ起動時1箇所で据える書き手。MainGameスコープに置くと、起動〜ゲート〜ロードの全区間が
    // The writer is installed at the same boot-time spot that consumes the marks; living in the MainGame scope left
    // 「消費済み・書き手未生成」になり、落ちていないのに次回起動が毎回「前回異常終了」になる
    // the whole boot→gate→load span as "consumed with no writer", making every next boot read as a crash
    public static class CleanExitMarkWriter
    {
        private static CompositeDisposable _subscriptions;

        public static void InstallAtStartup(int processId, string sessionName)
        {
            // 起動シーケンスは再入する（Editorの再生し直し）。購読は常に1組に保つ
            // The boot sequence re-enters (an Editor replay), so exactly one set of subscriptions is kept
            _subscriptions?.Dispose();
            _subscriptions = new CompositeDisposable();

            // 出所はこのセッション自身が開始時に書き残す。落ちた後の送信時に読むと、次に起動したビルドの値になる
            // The session writes its own origin at start; reading it at send time after a crash would yield whatever build launched next
            var origin = new SessionOriginSnapshot(PlaytestSessionIdentityProvider.Current.SteamId, RepositoryStateProbe.ReadBuildOrigin());
            CleanExitMarker.MarkSessionStarted(processId, sessionName, origin);

            // 終了処理側にプレイテストの語彙を持ち込まないため、直接呼び出しでなく汎用イベントの購読で受ける
            // Subscribing to the generic events, not a direct call, keeps playtest vocabulary out of the shutdown code
            var exitIntentDeclared = false;
            GameShutdownEvent.OnGameShutdown.Subscribe(OnShutdownDeclared).AddTo(_subscriptions);
            GameShutdownEvent.OnShutdownFlushed.Subscribe(OnShutdownFlushed).AddTo(_subscriptions);

            #region Internal

            // 意図的な終了だけを記録する。初期化失敗で畳む経路はクラッシュ側なので印を書かせない
            // Only a deliberate exit is recorded; the fold-up after a failed initialization is the crash side and writes nothing
            void OnShutdownDeclared(GameShutdownReason reason)
            {
                if (reason == GameShutdownReason.InitializationFailed)
                {
                    Debug.Log($"正常終了マーカーを書きません（終了理由: {reason}）。次回起動は前回異常終了として扱われます");
                    return;
                }
                exitIntentDeclared = true;
                CleanExitMarker.MarkExitIntent(processId, sessionName);

                // 待てない経路は書き出し完了の前にプロセスや再生が止まり、完了の印を置く機会が来ない。待たずに書かないと毎回偽のクラッシュになる
                // An unawaitable exit stops before the flush completes and never gets a chance to settle; not writing now would fake a crash every time
                if (reason != GameShutdownReason.UnawaitableExit) return;
                Debug.LogWarning("書き出し完了を待てない終了のため、意思表明の時点で正常終了として記録します（終了処理中の停止はこの経路では検知できません）");
                CleanExitMarker.MarkCleanExit(processId, sessionName);
            }

            // 正常終了の印は全参加者の書き出しが終わってから確定する。意思表明の時点で書くと、終了処理中のフリーズを検知できない（F03）
            // The clean mark is settled only after every participant's flush finishes; writing it at the intent would hide a freeze during shutdown (F03)
            void OnShutdownFlushed(ShutdownFlushResult result)
            {
                if (!exitIntentDeclared) return;
                if (result != ShutdownFlushResult.Flushed && result != ShutdownFlushResult.NothingFlushed)
                    Debug.LogWarning($"終了時の書き出しは {result} で終わりましたが、終了処理自体は完了したため正常終了として記録します");
                CleanExitMarker.MarkCleanExit(processId, sessionName);
            }

            #endregion
        }
    }
}
