using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Commands
{
    /// <summary>
    /// 拒否理由 1 つの性質と写し（良性か・ログの失敗コード・トーストの文言）
    /// One reject reason's nature and mappings (benign or not, log failure code, toast message)
    /// </summary>
    public sealed class UiCommandRejectReasonRow
    {
        // 状態を変えない再選択・受理外のタップ・古い応答・空の掴みのフィルタ設定などは良性でトーストに出さない
        // Re-selections, non-accepting taps, stale replies and filter sets with an empty grab are benign and never toast
        public bool IsBenign { get; }

        // ログと録画の照合に使う既定の失敗コード。拒否ごとの上書きは UiCommandRejected.ToWireCode が優先する
        // Default failure code for logs and recording checks; UiCommandRejected.ToWireCode prefers a per-rejection override
        public string WireCode { get; }

        // トーストに出す文言。失敗コードは利用者に意味が通じないので表示には使わない
        // Message shown in the toast; the failure code means nothing to players, so it is never displayed
        public LocalizationKey LocalizationKey { get; }

        public UiCommandRejectReasonRow(bool isBenign, string wireCode, LocalizationKey localizationKey)
        {
            IsBenign = isBenign;
            WireCode = wireCode;
            LocalizationKey = localizationKey;
        }
    }

    /// <summary>
    /// 拒否理由の表。1 行 1 理由で、enum の全値がちょうど 1 行ずつある
    /// Table of reject reasons: one row per reason, every enum value exactly once
    /// </summary>
    public static class UiCommandRejectReasonTable
    {
        // 列順: isBenign, wireCode, localizationKey
        // Column order: isBenign, wireCode, localizationKey
        private static readonly Dictionary<UiCommandRejectReason, UiCommandRejectReasonRow> Rows = BuildRows();

        public static UiCommandRejectReasonRow Get(UiCommandRejectReason reason)
        {
            return Rows[reason];
        }

        private static Dictionary<UiCommandRejectReason, UiCommandRejectReasonRow> BuildRows()
        {
            // 同じ理由の行が 2 つあれば初期化子の Add が止める
            // A duplicated reason stops at the initializer's Add
            var rows = new Dictionary<UiCommandRejectReason, UiCommandRejectReasonRow>
            {
                { UiCommandRejectReason.EmptyDescription, new UiCommandRejectReasonRow(false, "empty_description", LocalizationKeys.Ui.CommandRejected.EmptyDescription) },
                { UiCommandRejectReason.UnsubmittableReportKind, new UiCommandRejectReasonRow(false, "invalid_kind", LocalizationKeys.Ui.CommandRejected.UnsubmittableReportKind) },
                { UiCommandRejectReason.CaptureRefused, new UiCommandRejectReasonRow(false, "capture_refused", LocalizationKeys.Ui.CommandRejected.CaptureRefused) },
                { UiCommandRejectReason.BundleWriteFailed, new UiCommandRejectReasonRow(false, "bundle_write_failed", LocalizationKeys.Ui.CommandRejected.BundleWriteFailed) },
                { UiCommandRejectReason.UnknownLanguage, new UiCommandRejectReasonRow(false, "unknown_locale", LocalizationKeys.Ui.CommandRejected.UnknownLanguage) },
                { UiCommandRejectReason.LanguageAlreadySelected, new UiCommandRejectReasonRow(true, "already_selected", LocalizationKeys.Ui.CommandRejected.LanguageAlreadySelected) },
                { UiCommandRejectReason.ItemSourceSlotEmpty, new UiCommandRejectReasonRow(true, "empty_slot", LocalizationKeys.Ui.CommandRejected.ItemSourceSlotEmpty) },
                { UiCommandRejectReason.ItemSourceCountShort, new UiCommandRejectReasonRow(true, "insufficient_count", LocalizationKeys.Ui.CommandRejected.ItemSourceCountShort) },
                { UiCommandRejectReason.SplitGrabNotEmpty, new UiCommandRejectReasonRow(true, "grab_not_empty", LocalizationKeys.Ui.CommandRejected.SplitGrabNotEmpty) },
                { UiCommandRejectReason.SplitDragNoValidSlots, new UiCommandRejectReasonRow(false, "no_valid_slots", LocalizationKeys.Ui.CommandRejected.SplitDragNoValidSlots) },
                { UiCommandRejectReason.BlockSlotUnavailable, new UiCommandRejectReasonRow(false, "invalid_slot", LocalizationKeys.Ui.CommandRejected.BlockSlotUnavailable) },
                { UiCommandRejectReason.ItemUnknownInSlot, new UiCommandRejectReasonRow(false, "unknown_item", LocalizationKeys.Ui.CommandRejected.ItemUnknownInSlot) },
                { UiCommandRejectReason.ResearchFailed, new UiCommandRejectReasonRow(false, "research_failed", LocalizationKeys.Ui.CommandRejected.ResearchFailed) },
                { UiCommandRejectReason.BlockNotOpen, new UiCommandRejectReasonRow(false, "block_not_open", LocalizationKeys.Ui.CommandRejected.BlockNotOpen) },
                { UiCommandRejectReason.RecipeInvalid, new UiCommandRejectReasonRow(false, "invalid_recipe", LocalizationKeys.Ui.CommandRejected.RecipeInvalid) },
                { UiCommandRejectReason.RecipeLocked, new UiCommandRejectReasonRow(false, "recipe_locked", LocalizationKeys.Ui.CommandRejected.RecipeLocked) },
                { UiCommandRejectReason.MachineRecipeRequestFailed, new UiCommandRejectReasonRow(false, "machine_recipe_request_failed", LocalizationKeys.Ui.CommandRejected.MachineRecipeRequestFailed) },
                { UiCommandRejectReason.TrainTimetableReplaceFailed, new UiCommandRejectReasonRow(false, "replace_failed", LocalizationKeys.Ui.CommandRejected.TrainTimetableReplaceFailed) },
                { UiCommandRejectReason.TrainAutoRunFailed, new UiCommandRejectReasonRow(false, "set_auto_run_failed", LocalizationKeys.Ui.CommandRejected.TrainAutoRunFailed) },
                { UiCommandRejectReason.TrainAutoRunNotApplied, new UiCommandRejectReasonRow(false, "set_auto_run_not_applied", LocalizationKeys.Ui.CommandRejected.TrainAutoRunNotApplied) },
                { UiCommandRejectReason.BlockTypeMismatch, new UiCommandRejectReasonRow(false, "invalid_block_type", LocalizationKeys.Ui.CommandRejected.BlockTypeMismatch) },
                { UiCommandRejectReason.TrainStationNameFailed, new UiCommandRejectReasonRow(false, "set_name_failed", LocalizationKeys.Ui.CommandRejected.TrainStationNameFailed) },
                { UiCommandRejectReason.TrainPlatformModeFailed, new UiCommandRejectReasonRow(false, "mode_request_failed", LocalizationKeys.Ui.CommandRejected.TrainPlatformModeFailed) },
                { UiCommandRejectReason.SpecialBlockRequestFailed, new UiCommandRejectReasonRow(false, "special_block_request_failed", LocalizationKeys.Ui.CommandRejected.SpecialBlockRequestFailed) },
                { UiCommandRejectReason.SpecialBlockCommandStale, new UiCommandRejectReasonRow(true, "special_block_command_stale", LocalizationKeys.Ui.CommandRejected.SpecialBlockCommandStale) },
                { UiCommandRejectReason.FilterSplitterGrabEmpty, new UiCommandRejectReasonRow(true, "grab_empty", LocalizationKeys.Ui.CommandRejected.FilterSplitterGrabEmpty) },
                { UiCommandRejectReason.BuildMenuNotOpen, new UiCommandRejectReasonRow(true, "invalid_state", LocalizationKeys.Ui.CommandRejected.BuildMenuNotOpen) },
                { UiCommandRejectReason.UnknownBuildMenuEntry, new UiCommandRejectReasonRow(true, "unknown_entry", LocalizationKeys.Ui.CommandRejected.UnknownBuildMenuEntry) },
                { UiCommandRejectReason.BlueprintDeleteNotFound, new UiCommandRejectReasonRow(true, "blueprint_delete_not_found", LocalizationKeys.Ui.CommandRejected.BlueprintDeleteNotFound) },
                { UiCommandRejectReason.BlueprintDeleteNotUnlocked, new UiCommandRejectReasonRow(false, "blueprint_delete_not_unlocked", LocalizationKeys.Ui.CommandRejected.BlueprintDeleteNotUnlocked) },
                { UiCommandRejectReason.BlueprintDeleteRequestFailed, new UiCommandRejectReasonRow(false, "blueprint_delete_request_failed", LocalizationKeys.Ui.CommandRejected.BlueprintDeleteRequestFailed) },
                { UiCommandRejectReason.BlueprintDeleteUnknown, new UiCommandRejectReasonRow(false, "blueprint_delete_unknown", LocalizationKeys.Ui.CommandRejected.BlueprintDeleteUnknown) },
                { UiCommandRejectReason.ScreenCloseRefused, new UiCommandRejectReasonRow(false, "transition_not_allowed", LocalizationKeys.Ui.CommandRejected.ScreenCloseRefused) },
                { UiCommandRejectReason.HotbarSelectNotAccepted, new UiCommandRejectReasonRow(true, "invalid_state", LocalizationKeys.Ui.CommandRejected.HotbarSelectNotAccepted) },
                { UiCommandRejectReason.ModalResponseStale, new UiCommandRejectReasonRow(true, "no_pending_modal", LocalizationKeys.Ui.CommandRejected.ModalResponseStale) },
                { UiCommandRejectReason.SkitIntentRejected, new UiCommandRejectReasonRow(false, "skit_intent_rejected", LocalizationKeys.Ui.CommandRejected.SkitIntentRejected) },
            };

            // 理由を足して行を書き忘れたら静的初期化で止める
            // A new reason without a row stops at static initialization
            foreach (UiCommandRejectReason reason in Enum.GetValues(typeof(UiCommandRejectReason)))
            {
                if (!rows.ContainsKey(reason)) throw new InvalidOperationException($"[UiCommandRejectReasonTable] missing row {reason}");
            }
            return rows;
        }
    }
}
