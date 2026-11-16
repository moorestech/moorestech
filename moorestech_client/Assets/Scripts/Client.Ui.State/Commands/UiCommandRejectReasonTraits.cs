using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Commands
{
    /// <summary>
    /// 拒否理由の性質と写しの入口。中身は UiCommandRejectReasonTable の 1 行
    /// Entry to each reject reason's nature and mappings; the content is one row of UiCommandRejectReasonTable
    /// </summary>
    public static class UiCommandRejectReasonTraits
    {
        // 状態を変えない再選択・受理外のタップ・古い応答・空の掴みのフィルタ設定などは良性でトーストに出さない
        // Re-selections, non-accepting taps, stale replies and filter sets with an empty grab are benign and never toast
        public static bool IsBenign(UiCommandRejectReason reason)
        {
            return UiCommandRejectReasonTable.Get(reason).IsBenign;
        }

        // トーストに出す文言。失敗コードは利用者に意味が通じないので表示には使わない
        // Message shown in the toast; the failure code means nothing to players, so it is never displayed
        public static LocalizationKey ToLocalizationKey(UiCommandRejectReason reason)
        {
            return UiCommandRejectReasonTable.Get(reason).LocalizationKey;
        }

        // ログと録画の照合に使う失敗コード。上書きは UiCommandRejected の側が持つ
        // Failure code for logs and recording checks; the override lives on UiCommandRejected
        public static string ToWireCode(UiCommandRejectReason reason)
        {
            return UiCommandRejectReasonTable.Get(reason).WireCode;
        }
    }
}
