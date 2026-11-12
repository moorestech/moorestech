namespace Client.Ui.State.Commands
{
    /// <summary>
    /// 型付きコマンドの結果。受理か、理由つきの拒否かのどちらか
    /// Result of a typed command: either accepted or rejected with a reason
    /// </summary>
    public abstract class UiCommandResult
    {
    }

    public sealed class UiCommandAccepted : UiCommandResult
    {
        public static readonly UiCommandAccepted Instance = new();

        private UiCommandAccepted()
        {
        }
    }

    /// <summary>
    /// 拒否の結果。wire 上書きと診断は別の役（A-260）
    /// A rejection; wire override and diagnostic are separate (A-260)
    /// </summary>
    public sealed class UiCommandRejected : UiCommandResult
    {
        public UiCommandRejectReason Reason { get; }

        // Web へ返す失敗コード。無ければ空
        // Failure code returned to Web instead; empty if none
        private readonly string _wireCodeOverride;

        // 開発者ログだけの補足
        // Developer-log detail only; never returned to Web
        public string Diagnostic { get; }

        private UiCommandRejected(UiCommandRejectReason reason, string wireCodeOverride, string diagnostic)
        {
            Reason = reason;
            _wireCodeOverride = wireCodeOverride;
            Diagnostic = diagnostic;
        }

        public static UiCommandRejected Of(UiCommandRejectReason reason)
        {
            return new UiCommandRejected(reason, string.Empty, string.Empty);
        }

        public static UiCommandRejected OverridingWireCode(UiCommandRejectReason reason, string wireCode)
        {
            return new UiCommandRejected(reason, wireCode, string.Empty);
        }

        public static UiCommandRejected WithDiagnostic(UiCommandRejectReason reason, string diagnostic)
        {
            return new UiCommandRejected(reason, string.Empty, diagnostic);
        }

        // Web へ返す失敗コード。上書き優先
        // Wire failure code; the override wins
        public string ToWireCode()
        {
            return 0 < _wireCodeOverride.Length ? _wireCodeOverride : UiCommandRejectReasonTraits.ToWireCode(Reason);
        }
    }
}
