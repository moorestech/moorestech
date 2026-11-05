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

    public sealed class UiCommandRejected : UiCommandResult
    {
        public UiCommandRejectReason Reason { get; }

        // 送信手続きの失敗コード等の補足。無ければ空文字
        // Extra detail such as the submitter's failure code; empty when there is none
        public string Detail { get; }

        public UiCommandRejected(UiCommandRejectReason reason, string detail)
        {
            Reason = reason;
            Detail = detail;
        }

        // Web へ返す失敗コード。補足があればそれ（送信手続きの失敗コード）、無ければ理由の既定コード
        // Failure code returned to the Web: the detail (submitter failure code) when present, else the reason's code
        public string ToWireCode()
        {
            return 0 < Detail.Length ? Detail : UiCommandRejectReasonTraits.ToWireCode(Reason);
        }
    }
}
