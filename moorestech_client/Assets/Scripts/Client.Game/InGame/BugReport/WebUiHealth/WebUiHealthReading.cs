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
        Unknown,
    }

    // CEF の生死は判定しない。ObservedAfterBrowserStart は「ブラウザ生成後に観測を始め、喪失通知が来ていない」事実だけを表す。生死は secondsSinceLastWebMessage で読む
    // CEF liveness is not judged; ObservedAfterBrowserStart only states observation began after browser creation with no loss notice since; read liveness from secondsSinceLastWebMessage
    public enum CefProcessState
    {
        NotObserved,
        ObservedAfterBrowserStart,
        LostRestarting,
        RecoveryFailed,
        Unknown,
    }

    // manifest.webUiHealth の契約。UIの停止をCEFと決めつけないよう、判定でなく生の値だけを運ぶ（§3.11・D-11）
    // The manifest.webUiHealth contract; it carries raw values only, never a verdict, so a UI stall is not presumed to be CEF (§3.11, D-11)
    public sealed class WebUiHealthReading
    {
        // remoteExec（Pascal）と綴り規則が違うのは feature/uitk との blob 一致のため。enum 改名時は display_fields.py の綴り集合も直す
        // Spelling differs from remoteExec (Pascal) to keep the blob equal to feature/uitk; renaming an enum also needs display_fields.py's spelling sets fixed
        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        public WebUiHostStartupState HostStartup;

        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        public CefProcessState CefProcess;

        // 一度もWSを受信していなければ null。0 は「今受信した」という実値なので代わりに使わない
        // Null when no WS message was ever received; 0 would claim "just received", so it is never used instead
        public double? SecondsSinceLastWebMessage;

        // 前回セッションの値は記録が残っていない。未試行・未観測と書くと事実を偽るので「不明」で返す
        // The previous session's values were never recorded; notAttempted/notObserved would assert a fact, so this reads unknown
        public static WebUiHealthReading UnknownForPreviousSession()
        {
            return new WebUiHealthReading { HostStartup = WebUiHostStartupState.Unknown, CefProcess = CefProcessState.Unknown, SecondsSinceLastWebMessage = null };
        }
    }
}
