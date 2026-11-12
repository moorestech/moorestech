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
    /// 拒否の結果。Web へ返す失敗コードの上書きと開発者向けの診断は別の役で、作り方（factory）で分ける（A-260）
    /// A rejection; the wire code override and the developer diagnostic are separate roles, chosen by the factory used (A-260)
    /// </summary>
    public sealed class UiCommandRejected : UiCommandResult
    {
        public UiCommandRejectReason Reason { get; }

        // 理由の既定コードに代えて Web へ返す失敗コード（送信手続き・store・特殊族の応答の失敗コード）。無ければ空文字
        // Failure code returned to the Web instead of the reason's code (submitter, store or special block response code); empty when none
        public string WireCodeOverride { get; }

        // 開発者ログだけに出す補足（サーバーの enum 名等）。Web へは返さない。無ければ空文字
        // Extra detail only for the developer log (e.g. a server enum name); never returned to the Web; empty when none
        public string Diagnostic { get; }

        private UiCommandRejected(UiCommandRejectReason reason, string wireCodeOverride, string diagnostic)
        {
            Reason = reason;
            WireCodeOverride = wireCodeOverride;
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

        // Web へ返す失敗コード。上書きがあればそれ、無ければ理由の既定コード
        // Failure code returned to the Web: the override when present, else the reason's code
        public string ToWireCode()
        {
            return 0 < WireCodeOverride.Length ? WireCodeOverride : UiCommandRejectReasonTraits.ToWireCode(Reason);
        }
    }
}
