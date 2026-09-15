using System;
using System.IO;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 失敗した箱の試行回数。上限に達したら見送り印を打ち、後続の箱が永久に塞がれるのを防ぐ
    // Attempt counter per failed box; hitting the cap parks the box so it can never block later ones forever
    public static class PlaytestUploadAttemptLog
    {
        public const int MaxAttempts = 5;

        // 数えるのは再試行しても直らない失敗だけ。一時的な失敗は LogRetryable へ回す
        // Only failures that retrying cannot heal are counted; transient ones go to LogRetryable
        public static int Increment(string boxDirectory, string reason)
        {
            var path = Path.Combine(boxDirectory, PlaytestOutboxScanner.AttemptsMarker);
            var attempts = ReadAttempts() + 1;
            File.WriteAllText(path, $"{attempts}\n{reason}");

            if (MaxAttempts <= attempts)
            {
                MarkFailed();
                return attempts;
            }

            Debug.LogWarning($"[PlaytestReceiver] upload attempt {attempts}/{MaxAttempts} failed for {Path.GetFileName(boxDirectory)}: {reason}");
            return attempts;

            #region Internal

            int ReadAttempts()
            {
                if (!File.Exists(path)) return 0;
                var head = File.ReadAllText(path).Split('\n')[0];
                return int.TryParse(head, out var count) ? count : 0;
            }

            void MarkFailed()
            {
                File.WriteAllText(Path.Combine(boxDirectory, PlaytestOutboxScanner.FailedMarker), reason);
                Debug.LogError($"[PlaytestReceiver] giving up on {Path.GetFileName(boxDirectory)} after {MaxAttempts} attempts: {reason}. 手動で送る場合は rsync 経路を使うこと");
            }

            #endregion
        }

        // 一時的な失敗は回数を増やさない。いつまでも送れない箱を後から見分けられるよう理由だけログへ残す
        // A transient failure never raises the count; only its reason is logged so a box that never ships can be spotted later
        public static void LogRetryable(string boxDirectory, string reason)
        {
            Debug.LogWarning($"[PlaytestReceiver] {Path.GetFileName(boxDirectory)} deferred without counting an attempt: {reason}");
        }

        public static void MarkUploaded(string boxDirectory)
        {
            File.WriteAllText(Path.Combine(boxDirectory, PlaytestOutboxScanner.UploadedMarker), DateTime.UtcNow.ToString("o"));

            // 途中で失敗した回数は送れた時点で意味を失う。残すと箱を手で見たとき成否が読み取れない
            // The attempt count loses its meaning once the box is shipped; leaving it makes a hand-inspected box ambiguous
            var attempts = Path.Combine(boxDirectory, PlaytestOutboxScanner.AttemptsMarker);
            if (File.Exists(attempts)) File.Delete(attempts);
        }
    }
}
