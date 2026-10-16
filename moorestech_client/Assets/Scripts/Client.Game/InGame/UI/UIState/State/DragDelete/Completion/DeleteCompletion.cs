using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Completion
{
    public enum DeleteCompletionKind { Pending, Succeeded, Rejected, Unconfirmed }

    // 応答のない送信を成功に変換しない
    // Never turn an unacknowledged send into success
    public readonly struct DeleteCompletion
    {
        public readonly object TargetKey;
        public readonly DeleteCompletionKind Kind;
        public readonly LocalizationKey? DenyReason;

        public DeleteCompletion(object targetKey, DeleteCompletionKind kind, LocalizationKey? denyReason)
        {
            TargetKey = targetKey;
            Kind = kind;
            DenyReason = denyReason;
        }
    }
}
