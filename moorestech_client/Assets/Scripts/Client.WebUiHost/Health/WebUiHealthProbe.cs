using System.Diagnostics;
using Client.Game.InGame.BugReport.WebUiHealth;
using Client.Input;

namespace Client.WebUiHost.Health
{
    // 記録済みの生の値をバグ報告の形へ詰めるだけ。判定はしない
    // Only packs the recorded raw values into the bug report's shape; it makes no verdict
    public sealed class WebUiHealthProbe : IWebUiHealthProbe
    {
        public WebUiHealthReading Read()
        {
            return new WebUiHealthReading
            {
                HostStartup = WebUiHealthRecorder.HostStartup,
                CefProcess = WebUiHealthRecorder.CefProcess,
                SecondsSinceLastWebMessage = WebUiInputExclusivity.SecondsSinceLastWebMessage(Stopwatch.GetTimestamp()),
            };
        }
    }
}
