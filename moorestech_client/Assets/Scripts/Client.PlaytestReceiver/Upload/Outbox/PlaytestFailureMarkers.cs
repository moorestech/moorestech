using System.IO;

namespace Client.PlaytestReceiver.Upload
{
    // 失敗印解除の正本。再送・容量保留解除・起動時補完が共有
    // The single routine removing failure markers; resend, capacity-hold release and startup completion share it
    public static class PlaytestFailureMarkers
    {
        public static void Clear(string boxDirectory)
        {
            // 失敗印を最後に消す。途中で失敗しても箱はFailedのまま残り、報告と状態が一致する
            // Remove the failure marker last so a mid-way failure leaves the box Failed, matching the reported result
            File.Delete(Path.Combine(boxDirectory, PlaytestOutboxScanner.AttemptsMarker));
            File.Delete(Path.Combine(boxDirectory, PlaytestOutboxScanner.FailedMarker));
        }
    }
}
