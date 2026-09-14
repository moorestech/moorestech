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

        public static int Increment(string boxDirectory, string reason)
        {
            var path = Path.Combine(boxDirectory, PlaytestOutboxScanner.AttemptsMarker);
            var attempts = Read(path) + 1;
            File.WriteAllText(path, $"{attempts}\n{reason}");

            if (attempts >= MaxAttempts)
            {
                MarkFailed(boxDirectory, reason);
                return attempts;
            }

            Debug.LogWarning($"[PlaytestReceiver] upload attempt {attempts}/{MaxAttempts} failed for {Path.GetFileName(boxDirectory)}: {reason}");
            return attempts;
        }

        public static void MarkFailed(string boxDirectory, string reason)
        {
            File.WriteAllText(Path.Combine(boxDirectory, PlaytestOutboxScanner.FailedMarker), reason);
            Debug.LogError($"[PlaytestReceiver] giving up on {Path.GetFileName(boxDirectory)} after {MaxAttempts} attempts: {reason}. 手動で送る場合は rsync 経路を使うこと");
        }

        public static void MarkUploaded(string boxDirectory)
        {
            File.WriteAllText(Path.Combine(boxDirectory, PlaytestOutboxScanner.UploadedMarker), DateTime.UtcNow.ToString("o"));
        }

        private static int Read(string path)
        {
            if (!File.Exists(path)) return 0;
            var head = File.ReadAllText(path).Split('\n')[0];
            return int.TryParse(head, out var attempts) ? attempts : 0;
        }
    }
}
