using System;
using Client.Game.InGame.BugReport.Submit;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Commands
{
    public enum UiCommandRejectReason
    {
        EmptyDescription,
        UnsubmittableReportKind,
        CaptureRefused,
        BundleWriteFailed,
        UnknownLanguage,
        LanguageAlreadySelected,
        ItemSourceSlotEmpty,
        ItemSourceCountShort,
        SplitGrabNotEmpty,
        SplitDragNoValidSlots,
        BlockSlotUnavailable,
        ItemUnknownInSlot,
        ResearchFailed,
    }

    /// <summary>
    /// 拒否理由の性質（良性か）と、Web の失敗コード・GameUi の表示文言への写し
    /// Nature of each reject reason (benign or not) and its mappings to the Web failure code and the GameUi message
    /// </summary>
    public static class UiCommandRejectReasonTraits
    {
        // 選択済み再選択と、Web の BENIGN_ERRORS にある持ち物の移動・分割の取りこぼしは良性
        // Re-selecting a chosen language and the inventory move/split misses in Web BENIGN_ERRORS are benign
        public static bool IsBenign(UiCommandRejectReason reason)
        {
            return reason switch
            {
                UiCommandRejectReason.EmptyDescription => false,
                UiCommandRejectReason.UnsubmittableReportKind => false,
                UiCommandRejectReason.CaptureRefused => false,
                UiCommandRejectReason.BundleWriteFailed => false,
                UiCommandRejectReason.UnknownLanguage => false,
                UiCommandRejectReason.LanguageAlreadySelected => true,
                UiCommandRejectReason.ItemSourceSlotEmpty => true,
                UiCommandRejectReason.ItemSourceCountShort => true,
                UiCommandRejectReason.SplitGrabNotEmpty => true,
                UiCommandRejectReason.SplitDragNoValidSlots => false,
                UiCommandRejectReason.BlockSlotUnavailable => false,
                UiCommandRejectReason.ItemUnknownInSlot => false,
                UiCommandRejectReason.ResearchFailed => false,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }

        // 送信手続きの失敗コードを拒否理由へ写す。書き出し失敗以外の拒否はどれも確保セッションの拒否
        // Maps the submitter's failure code to a reject reason; every refusal other than a write failure comes from the capture session
        public static UiCommandRejectReason FromSubmitFailureCode(string failureCode)
        {
            return failureCode == BugReportSubmitResult.BundleWriteFailed ? UiCommandRejectReason.BundleWriteFailed : UiCommandRejectReason.CaptureRefused;
        }

        // GameUi のトーストに出す文言。Web の失敗コードは利用者に意味が通じないので表示には使わない
        // Message shown in the GameUi toast; the Web failure code means nothing to players, so it is never displayed
        public static LocalizationKey ToLocalizationKey(UiCommandRejectReason reason)
        {
            return reason switch
            {
                UiCommandRejectReason.EmptyDescription => LocalizationKeys.Ui.CommandRejected.EmptyDescription,
                UiCommandRejectReason.UnsubmittableReportKind => LocalizationKeys.Ui.CommandRejected.UnsubmittableReportKind,
                UiCommandRejectReason.CaptureRefused => LocalizationKeys.Ui.CommandRejected.CaptureRefused,
                UiCommandRejectReason.BundleWriteFailed => LocalizationKeys.Ui.CommandRejected.BundleWriteFailed,
                UiCommandRejectReason.UnknownLanguage => LocalizationKeys.Ui.CommandRejected.UnknownLanguage,
                UiCommandRejectReason.LanguageAlreadySelected => LocalizationKeys.Ui.CommandRejected.LanguageAlreadySelected,
                UiCommandRejectReason.ItemSourceSlotEmpty => LocalizationKeys.Ui.CommandRejected.ItemSourceSlotEmpty,
                UiCommandRejectReason.ItemSourceCountShort => LocalizationKeys.Ui.CommandRejected.ItemSourceCountShort,
                UiCommandRejectReason.SplitGrabNotEmpty => LocalizationKeys.Ui.CommandRejected.SplitGrabNotEmpty,
                UiCommandRejectReason.SplitDragNoValidSlots => LocalizationKeys.Ui.CommandRejected.SplitDragNoValidSlots,
                UiCommandRejectReason.BlockSlotUnavailable => LocalizationKeys.Ui.CommandRejected.BlockSlotUnavailable,
                UiCommandRejectReason.ItemUnknownInSlot => LocalizationKeys.Ui.CommandRejected.ItemUnknownInSlot,
                UiCommandRejectReason.ResearchFailed => LocalizationKeys.Ui.CommandRejected.ResearchFailed,
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
                UiCommandRejectReason.LanguageAlreadySelected => "already_selected",
                UiCommandRejectReason.CaptureRefused => "capture_refused",
                UiCommandRejectReason.BundleWriteFailed => BugReportSubmitResult.BundleWriteFailed,
                UiCommandRejectReason.ItemSourceSlotEmpty => "empty_slot",
                UiCommandRejectReason.ItemSourceCountShort => "insufficient_count",
                UiCommandRejectReason.SplitGrabNotEmpty => "grab_not_empty",
                UiCommandRejectReason.SplitDragNoValidSlots => "no_valid_slots",
                UiCommandRejectReason.BlockSlotUnavailable => "invalid_slot",
                UiCommandRejectReason.ItemUnknownInSlot => "unknown_item",
                UiCommandRejectReason.ResearchFailed => "research_failed",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
