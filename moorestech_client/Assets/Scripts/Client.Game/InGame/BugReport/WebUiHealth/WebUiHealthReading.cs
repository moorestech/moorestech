using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Client.Game.InGame.BugReport.WebUiHealth
{
    public enum WebUiHostStartupState
    {
        NotAttempted,
        Started,
        Failed,
        SkippedByEditorSwitch,
    }

    public enum CefProcessState
    {
        NotObserved,
        Running,
        LostRestarting,
        RecoveryFailed,
    }

    // manifest.webUiHealth の契約。UIの停止をCEFと決めつけないよう、判定でなく生の値だけを運ぶ（§3.11・D-11）
    // The manifest.webUiHealth contract; it carries raw values only, never a verdict, so a UI stall is not presumed to be CEF (§3.11, D-11)
    public sealed class WebUiHealthReading
    {
        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        public WebUiHostStartupState HostStartup;

        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        public CefProcessState CefProcess;

        // 一度もWSを受信していなければ null。0 は「今受信した」という実値なので代わりに使わない
        // Null when no WS message was ever received; 0 would claim "just received", so it is never used instead
        public double? SecondsSinceLastWebMessage;
    }
}
