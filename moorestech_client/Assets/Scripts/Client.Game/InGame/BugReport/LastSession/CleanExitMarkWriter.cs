using System;
using Client.Game.Common;
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
        private static IDisposable _subscription;

        public static void InstallAtStartup(int processId)
        {
            // 起動シーケンスは再入する（メインメニューへ戻って再度開始）。購読は常に1本に保つ
            // The boot sequence re-enters (back to the main menu, then start again), so exactly one subscription is kept
            _subscription?.Dispose();

            CleanExitMarker.MarkSessionStarted(processId);

            // 意図的な終了だけを正常終了として記録する。初期化失敗で畳む経路はクラッシュ側なので印を書かせない
            // Only a deliberate exit counts as clean; the fold-up after a failed initialization is the crash side and writes nothing
            _subscription = GameShutdownEvent.OnGameShutdown.Subscribe(reason =>
            {
                if (reason != GameShutdownReason.IntentionalExit)
                {
                    Debug.Log($"正常終了マーカーを書きません（終了理由: {reason}）。次回起動は前回異常終了として扱われます");
                    return;
                }
                CleanExitMarker.MarkCleanExit(processId);
            });
        }
    }
}
