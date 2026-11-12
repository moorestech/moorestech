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
        BlockNotOpen,
        MachineRecipeInvalid,
        MachineRecipeLocked,
        MachineRecipeRequestFailed,
        TrainTimetableReplaceFailed,
        TrainAutoRunFailed,
        TrainAutoRunNotApplied,
        TrainInvalidBlockType,
        TrainStationNameFailed,
        TrainPlatformModeFailed,
        SpecialBlockRequestFailed,
        SpecialBlockCommandStale,
        FilterSplitterGrabEmpty,
        BuildMenuNotOpen,
        UnknownBuildMenuEntry,
        BlueprintDeleteNotFound,
        BlueprintDeleteNotUnlocked,
        BlueprintDeleteRequestFailed,
        BlueprintDeleteUnknown,
        BuildMenuCloseRefused,
        HotbarSelectNotAccepted,
        ModalResponseStale,
        SkitIntentRejected,
    }

    /// <summary>
    /// 拒否理由の性質（良性か）と、Web の失敗コード・GameUi の表示文言への写し
    /// Nature of each reject reason (benign or not) and its mappings to the Web failure code and the GameUi message
    /// </summary>
    public static class UiCommandRejectReasonTraits
    {
        // 状態を変えない再選択・受理外のタップ・古い応答と、Web の BENIGN_ERRORS にある取りこぼし（特殊族の古い操作含む）と、Web が送らない空の掴みのフィルタ設定は良性
        // Re-selections, non-accepting taps and stale replies that change no state, the misses in Web BENIGN_ERRORS (incl. special-family stale operations), and a filter set with an empty grab (never sent by Web) are benign
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
                UiCommandRejectReason.BlockNotOpen => false,
                UiCommandRejectReason.MachineRecipeInvalid => false,
                UiCommandRejectReason.MachineRecipeLocked => false,
                UiCommandRejectReason.MachineRecipeRequestFailed => false,
                UiCommandRejectReason.TrainTimetableReplaceFailed => false,
                UiCommandRejectReason.TrainAutoRunFailed => false,
                UiCommandRejectReason.TrainAutoRunNotApplied => false,
                UiCommandRejectReason.TrainInvalidBlockType => false,
                UiCommandRejectReason.TrainStationNameFailed => false,
                UiCommandRejectReason.TrainPlatformModeFailed => false,
                UiCommandRejectReason.SpecialBlockRequestFailed => false,
                UiCommandRejectReason.SpecialBlockCommandStale => true,
                UiCommandRejectReason.FilterSplitterGrabEmpty => true,
                UiCommandRejectReason.BuildMenuNotOpen => true,
                UiCommandRejectReason.UnknownBuildMenuEntry => true,
                UiCommandRejectReason.BlueprintDeleteNotFound => true,
                UiCommandRejectReason.BlueprintDeleteNotUnlocked => false,
                UiCommandRejectReason.BlueprintDeleteRequestFailed => false,
                UiCommandRejectReason.BlueprintDeleteUnknown => false,
                UiCommandRejectReason.BuildMenuCloseRefused => false,
                UiCommandRejectReason.HotbarSelectNotAccepted => true,
                UiCommandRejectReason.ModalResponseStale => true,
                UiCommandRejectReason.SkitIntentRejected => false,
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
                UiCommandRejectReason.BlockNotOpen => LocalizationKeys.Ui.CommandRejected.BlockNotOpen,
                UiCommandRejectReason.MachineRecipeInvalid => LocalizationKeys.Ui.CommandRejected.MachineRecipeInvalid,
                UiCommandRejectReason.MachineRecipeLocked => LocalizationKeys.Ui.CommandRejected.MachineRecipeLocked,
                UiCommandRejectReason.MachineRecipeRequestFailed => LocalizationKeys.Ui.CommandRejected.MachineRecipeRequestFailed,
                UiCommandRejectReason.TrainTimetableReplaceFailed => LocalizationKeys.Ui.CommandRejected.TrainTimetableReplaceFailed,
                UiCommandRejectReason.TrainAutoRunFailed => LocalizationKeys.Ui.CommandRejected.TrainAutoRunFailed,
                UiCommandRejectReason.TrainAutoRunNotApplied => LocalizationKeys.Ui.CommandRejected.TrainAutoRunNotApplied,
                UiCommandRejectReason.TrainInvalidBlockType => LocalizationKeys.Ui.CommandRejected.TrainInvalidBlockType,
                UiCommandRejectReason.TrainStationNameFailed => LocalizationKeys.Ui.CommandRejected.TrainStationNameFailed,
                UiCommandRejectReason.TrainPlatformModeFailed => LocalizationKeys.Ui.CommandRejected.TrainPlatformModeFailed,
                UiCommandRejectReason.SpecialBlockRequestFailed => LocalizationKeys.Ui.CommandRejected.SpecialBlockRequestFailed,
                UiCommandRejectReason.SpecialBlockCommandStale => LocalizationKeys.Ui.CommandRejected.SpecialBlockCommandStale,
                UiCommandRejectReason.FilterSplitterGrabEmpty => LocalizationKeys.Ui.CommandRejected.FilterSplitterGrabEmpty,
                UiCommandRejectReason.BuildMenuNotOpen => LocalizationKeys.Ui.CommandRejected.BuildMenuNotOpen,
                UiCommandRejectReason.UnknownBuildMenuEntry => LocalizationKeys.Ui.CommandRejected.UnknownBuildMenuEntry,
                UiCommandRejectReason.BlueprintDeleteNotFound => LocalizationKeys.Ui.CommandRejected.BlueprintDeleteNotFound,
                UiCommandRejectReason.BlueprintDeleteNotUnlocked => LocalizationKeys.Ui.CommandRejected.BlueprintDeleteNotUnlocked,
                UiCommandRejectReason.BlueprintDeleteRequestFailed => LocalizationKeys.Ui.CommandRejected.BlueprintDeleteRequestFailed,
                UiCommandRejectReason.BlueprintDeleteUnknown => LocalizationKeys.Ui.CommandRejected.BlueprintDeleteUnknown,
                UiCommandRejectReason.BuildMenuCloseRefused => LocalizationKeys.Ui.CommandRejected.BuildMenuCloseRefused,
                UiCommandRejectReason.HotbarSelectNotAccepted => LocalizationKeys.Ui.CommandRejected.HotbarSelectNotAccepted,
                UiCommandRejectReason.ModalResponseStale => LocalizationKeys.Ui.CommandRejected.ModalResponseStale,
                UiCommandRejectReason.SkitIntentRejected => LocalizationKeys.Ui.CommandRejected.SkitIntentRejected,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }

        // 理由ごとの Web の失敗コード。送信手続き由来の 2 種と SkitIntentRejected（store の失敗コード）は Detail が優先される（UiCommandRejected.ToWireCode）
        // Web failure code per reason; for the two submitter-derived reasons and SkitIntentRejected (the store's failure code) the Detail wins (UiCommandRejected.ToWireCode)
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
                UiCommandRejectReason.BlockNotOpen => "block_not_open",
                UiCommandRejectReason.MachineRecipeInvalid => "invalid_recipe",
                UiCommandRejectReason.MachineRecipeLocked => "recipe_locked",
                UiCommandRejectReason.MachineRecipeRequestFailed => "machine_recipe_request_failed",
                UiCommandRejectReason.TrainTimetableReplaceFailed => "replace_failed",
                UiCommandRejectReason.TrainAutoRunFailed => "set_auto_run_failed",
                UiCommandRejectReason.TrainAutoRunNotApplied => "set_auto_run_not_applied",
                UiCommandRejectReason.TrainInvalidBlockType => "invalid_block_type",
                UiCommandRejectReason.TrainStationNameFailed => "set_name_failed",
                UiCommandRejectReason.TrainPlatformModeFailed => "mode_request_failed",
                UiCommandRejectReason.SpecialBlockRequestFailed => "special_block_request_failed",
                UiCommandRejectReason.SpecialBlockCommandStale => "special_block_command_stale",
                UiCommandRejectReason.FilterSplitterGrabEmpty => "grab_empty",
                UiCommandRejectReason.BuildMenuNotOpen => "invalid_state",
                UiCommandRejectReason.UnknownBuildMenuEntry => "unknown_entry",
                UiCommandRejectReason.BlueprintDeleteNotFound => "blueprint_delete_not_found",
                UiCommandRejectReason.BlueprintDeleteNotUnlocked => "blueprint_delete_not_unlocked",
                UiCommandRejectReason.BlueprintDeleteRequestFailed => "blueprint_delete_request_failed",
                UiCommandRejectReason.BlueprintDeleteUnknown => "blueprint_delete_unknown",
                UiCommandRejectReason.BuildMenuCloseRefused => "transition_not_allowed",
                UiCommandRejectReason.HotbarSelectNotAccepted => "invalid_state",
                UiCommandRejectReason.ModalResponseStale => "no_pending_modal",
                UiCommandRejectReason.SkitIntentRejected => "skit_intent_rejected",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
