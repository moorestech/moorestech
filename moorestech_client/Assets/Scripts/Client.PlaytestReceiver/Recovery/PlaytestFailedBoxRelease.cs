using System;
using System.IO;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    // 送れなかった箱の失敗印と試行回数だけを外し、次の走行の対象へ戻す。宣言済み・見送りの記録は残す
    // Removes only the failure and attempt markers so the next run picks the box up again; declaration and skip records stay
    public static class PlaytestFailedBoxRelease
    {
        public static int ReleaseAll(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope)
        {
            var released = Release(directories.ReportOutbox);
            if (scope.IncludesProgress()) released += Release(directories.ProgressOutbox);
            Debug.Log($"[PlaytestReceiver] released {released} failed box(es) for resend");
            return released;

            #region Internal

            int Release(string outbox)
            {
                var count = 0;
                foreach (var box in PlaytestOutboxScanner.EnumerateBoxDirectories(outbox))
                {
                    if (IsFailed(box) && TryRelease(box)) count++;
                }
                return count;
            }

            bool IsFailed(string box)
            {
                // 外部ディスク境界。読めない箱1つで残りの箱の再送を止めない
                // External disk boundary: one unreadable box never stops the rest from being resent
                try { return PlaytestOutboxBoxState.Classify(box) == PlaytestOutboxBoxKind.Failed; }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"[PlaytestReceiver] cannot release {box}: {error.Message}");
                    return false;
                }
            }

            bool TryRelease(string box)
            {
                var failed = Path.Combine(box, PlaytestOutboxScanner.FailedMarker);
                // 印の位置を塞ぐディレクトリは消さない。中身が箱の資料かもしれないため
                // A directory occupying the marker is never deleted because it may hold box payload
                if (Directory.Exists(failed))
                {
                    Debug.LogWarning($"[PlaytestReceiver] cannot release {box}: the failure marker is a directory");
                    return false;
                }
                // 外部ディスクIO境界。ロック・権限で消せない箱は失敗のまま残り、件数表示に出続ける
                // External disk IO boundary: a box that cannot be unmarked stays failed and keeps showing in the count
                try
                {
                    PlaytestFailureMarkers.Clear(box);
                    return true;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"[PlaytestReceiver] cannot release {box}: {error.Message}");
                    return false;
                }
            }

            #endregion
        }
    }
}
