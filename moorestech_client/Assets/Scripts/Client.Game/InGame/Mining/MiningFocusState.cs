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

            switch (outcome)
            {
                case MiningStartOutcome.Unavailable:
                    return new MiningIdleState();
                case MiningStartOutcome.InstantPickUp:
                    return PickUpProcess(context);
                case MiningStartOutcome.HandMiningNotAllowed:
                    // 掘れない理由を出して維持する
                    // Show why it cannot be mined and keep focus
                    MouseCursorTooltip.Instance.Show(LocalizationKeys.Ui.Tooltip.CannotHandMine);
                    return this;
                case MiningStartOutcome.ToolMismatch:
                    // 無効装備ならフォーカス維持
                    // Keep focus for invalid equipment
                    ShowRecommendMiningTools(recommendedToolItemIds);
                    return this;
            }

            // クリックしていない場合はフォーカスを維持
            // If not clicked, maintain focus
            if (!InputManager.Playable.ScreenLeftClick.GetKey)
            {
                MouseCursorTooltip.Instance.Show(LocalizationKeys.Ui.Tooltip.HoldToGet);
                return this;
            }

            // マイニング状態に遷移
            // Transition to mining state
            MouseCursorTooltip.Instance.Hide();
            return new MiningProgressState(currentTarget, usableMiningTool);

            #region Internal

            IMiningState PickUpProcess(MiningControllerContext pickUpContext)
            {
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown)
                {
                    MouseCursorTooltip.Instance.Hide();
                    return new MiningCompleteState(pickUpContext.CurrentFocusTarget);
                }

                // 左クリックがされていなければ現状を維持
                // If left click is not pressed, maintain the current state
                MouseCursorTooltip.Instance.Show(LocalizationKeys.Ui.Tooltip.PickUpLeftClick);
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
                MouseCursorTooltip.Instance.Show(
                    LocalizationKeys.Ui.Tooltip.RequiredItems,
                    new[] { string.Join(", ", localizedToolNames) });
            }

            #endregion
        }
    }
}
