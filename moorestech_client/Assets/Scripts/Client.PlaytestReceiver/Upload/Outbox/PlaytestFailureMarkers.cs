using System.IO;

namespace Client.PlaytestReceiver.Upload
{
    // 失敗印と試行回数を外して箱を次の走行へ戻す処理の正本。再送・容量保留の解除・起動時補完が共有する
    // The single routine that removes the failure and attempt markers; resend, capacity-hold release and startup completion share it
    public static class PlaytestFailureMarkers
    {
        public static void Clear(string boxDirectory)
        {
            File.Delete(Path.Combine(boxDirectory, PlaytestOutboxScanner.FailedMarker));
            File.Delete(Path.Combine(boxDirectory, PlaytestOutboxScanner.AttemptsMarker));
        }
    }
}
