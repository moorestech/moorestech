using System;
using Client.Game.InGame.BugReport.Submit;

namespace Client.Ui.State.Commands
{
    public enum UiCommandRejectReason
    {
        EmptyDescription,
        UnsubmittableReportKind,
        CaptureRefused,
        BundleWriteFailed,
        UnknownLanguage,
    }

    /// <summary>
    /// 拒否理由の性質（良性か）と Web の失敗コードへの写し
    /// Nature of each reject reason (benign or not) and its mapping to the Web failure code
    /// </summary>
    public static class UiCommandRejectReasonTraits
    {
        // 良性の拒否は人に知らせない。今の拒否はどれも利用者に見せるべき失敗
        // Benign rejections are not surfaced; every current rejection is a failure the player should see
        public static bool IsBenign(UiCommandRejectReason reason)
        {
            return reason switch
            {
                UiCommandRejectReason.EmptyDescription => false,
                UiCommandRejectReason.UnsubmittableReportKind => false,
                UiCommandRejectReason.CaptureRefused => false,
                UiCommandRejectReason.BundleWriteFailed => false,
                UiCommandRejectReason.UnknownLanguage => false,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }

        // 理由ごとの Web の失敗コード。送信手続き由来の 2 種は Detail（失敗コード）が優先される（UiCommandRejected.ToWireCode）
        // Web failure code per reason; for the two submitter-derived reasons the Detail failure code wins (UiCommandRejected.ToWireCode)
        public static string ToWireCode(UiCommandRejectReason reason)
        {
            return reason switch
            {
                UiCommandRejectReason.EmptyDescription => "empty_description",
                UiCommandRejectReason.UnsubmittableReportKind => "invalid_kind",
                UiCommandRejectReason.UnknownLanguage => "unknown_locale",
                UiCommandRejectReason.CaptureRefused => "capture_refused",
                UiCommandRejectReason.BundleWriteFailed => BugReportSubmitResult.BundleWriteFailed,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
