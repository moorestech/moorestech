using System.IO;
using System.Linq;

namespace Client.PlaytestReceiver.Upload
{
    public enum PlaytestOutboxBoxKind { NotReady, Delivered, Pending, Failed }

    // 箱1つの配送状態の正本。件数表示・再送・書き出しはここだけで箱を分類する
    // The single source of a box's delivery state; inventory, resend and export classify boxes only here
    public static class PlaytestOutboxBoxState
    {
        public static PlaytestOutboxBoxKind Classify(string boxDirectory)
        {
            // File.Existsは権限拒否もfalseへ畳むため、箱の読取成功を先に確定する
            // File.Exists folds access denial into false, so verify box enumeration before classifying markers
            var markers = Directory.GetFiles(boxDirectory).Select(Path.GetFileName).ToArray();
            if (!markers.Contains(PlaytestOutboxScanner.ReadyMarker)) return PlaytestOutboxBoxKind.NotReady;
            if (markers.Contains(PlaytestOutboxScanner.UploadedMarker) || markers.Contains(PlaytestOutboxScanner.ShippedMarker)) return PlaytestOutboxBoxKind.Delivered;

            // 印を書けない同名ディレクトリも失敗として画面へ残す
            // A directory blocking the failure marker also remains visible as failed
            var blockedMarker = Directory.Exists(Path.Combine(boxDirectory, PlaytestOutboxScanner.FailedMarker));
            if (blockedMarker) UnityEngine.Debug.LogWarning($"[PlaytestReceiver] failure marker is a directory: {boxDirectory}");
            return blockedMarker || markers.Contains(PlaytestOutboxScanner.FailedMarker) ? PlaytestOutboxBoxKind.Failed : PlaytestOutboxBoxKind.Pending;
        }
    }
}
