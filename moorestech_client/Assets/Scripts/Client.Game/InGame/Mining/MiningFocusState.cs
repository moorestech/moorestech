using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.Mining
{
    public class MiningFocusState : IMiningState
    {
        public IMiningState GetNextUpdate(MiningControllerContext context, float dt)
        {
            // フォーカスが外れたのであればIdleに遷移
            // If the focus is lost, transition to Idle
            var currentTarget = context.CurrentFocusTarget;
            if (currentTarget == null) return new MiningIdleState();

            // 装備を渡して可否・種別・ツールを一度に問い合わせる
            // Ask once for availability, kind and tool with the equipment applied
            var equippedItemId = context.LocalPlayerEquipment.SelectedItem.Id;
            var outcome = currentTarget.TryBeginHandMining(equippedItemId, out var usableMiningTool, out var recommendedToolItemIds);
            var earnItemNames = context.CurrentFocusTargetEarnItemNames;

            switch (outcome)
            {
                case MiningStartOutcome.Unavailable:
                    return new MiningIdleState();
                case MiningStartOutcome.InstantPickUp:
                    return PickUpProcess(context);
                case MiningStartOutcome.HandMiningNotAllowed:
                    // 掘れない理由を出して維持する
                    // Show why it cannot be mined and keep focus
                    ShowEarnItemNamed(LocalizationKeys.Ui.Tooltip.NamedCannotHandMine, LocalizationKeys.Ui.Tooltip.CannotHandMine);
                    return this;
                case MiningStartOutcome.ToolMismatch:
                    // 無効装備ならフォーカス維持
                    // Keep focus for invalid equipment
                    ShowRecommendMiningTools(recommendedToolItemIds);
                    return this;
            }

            // Fが押されていない場合はフォーカスを維持
            // Keep focus while F is not held
            if (!InputManager.Playable.Interact.GetKey)
            {
                ShowEarnItemNamed(LocalizationKeys.Ui.Tooltip.NamedMineHold, LocalizationKeys.Ui.Tooltip.HoldToGet);
                return this;
            }

            // マイニング状態に遷移
            // Transition to mining state
            MouseCursorTooltip.Instance.Hide(MiningControllerContext.TooltipOwner);
            return new MiningProgressState(currentTarget, usableMiningTool);

            #region Internal

            IMiningState PickUpProcess(MiningControllerContext pickUpContext)
            {
                if (InputManager.Playable.Interact.GetKeyDown)
                {
                    MouseCursorTooltip.Instance.Hide(MiningControllerContext.TooltipOwner);
                    return new MiningCompleteState(pickUpContext.CurrentFocusTarget);
                }

                // Fが押されていなければ現状を維持
                // Keep the current state while F is not pressed
                ShowEarnItemNamed(LocalizationKeys.Ui.Tooltip.NamedMineClick, LocalizationKeys.Ui.Tooltip.PickUpInteract);
                return this;
            }

            void ShowRecommendMiningTools(List<ItemId> toolItemIds)
            {
                var localizedToolNames = new List<string>();
                foreach (var toolItemId in toolItemIds)
                {
                    var toolItemGuid = MasterHolder.ItemMaster.GetItemMaster(toolItemId).ItemGuid;
                    localizedToolNames.Add(Localize.GetContent(
                        ContentLocalizationKeys.ItemName(toolItemGuid)));
                }

                // 必要アイテム名をパラメータにまとめ、文言全体は表示側で解決する
                // Join required item names as a parameter and let the presentation resolve the full sentence
                var requiredItemNames = string.Join(", ", localizedToolNames);
                if (earnItemNames.Length == 0)
                {
                    MouseCursorTooltip.Instance.Show(
                        MiningControllerContext.TooltipOwner,
                        LocalizationKeys.Ui.Tooltip.RequiredItems,
                        new[] { requiredItemNames });
                    return;
                }

                MouseCursorTooltip.Instance.Show(
                    MiningControllerContext.TooltipOwner,
                    LocalizationKeys.Ui.Tooltip.NamedRequiredItems,
                    new[] { earnItemNames, requiredItemNames });
            }

            // 取得物名が無ければ従来文言へ戻す（ADR 0033）
            // Fall back to the nameless sentence when the target yields nothing (ADR 0033)
            void ShowEarnItemNamed(LocalizationKey namedKey, LocalizationKey unnamedKey)
            {
                if (earnItemNames.Length == 0)
                {
                    MouseCursorTooltip.Instance.Show(MiningControllerContext.TooltipOwner, unnamedKey);
                    return;
                }

                MouseCursorTooltip.Instance.Show(MiningControllerContext.TooltipOwner, namedKey, new[] { earnItemNames });
            }

            #endregion
        }
    }
}
