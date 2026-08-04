using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningFocusState : IMapObjectMiningState
    {
        public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
        {
            // フォーカスが外れたのであればIdleに遷移
            // If the focus is lost, transition to Idle
            var currentTarget = context.CurrentFocusTarget;
            if (currentTarget == null || !currentTarget.IsAvailable)
            {
                return new MapObjectMiningIdleState();
            }

            if (currentTarget.IsPickUp)
            {
                return PickUpProcess(context);
            }

            return MiningProcess(context);
        }
        
        private IMapObjectMiningState PickUpProcess(MapObjectMiningControllerContext context)
        {
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown)
            {
                MouseCursorTooltip.Instance.Hide();
                return new MapObjectMiningMiningCompleteState(context.CurrentFocusTarget);
            }
            
            // 左クリックがされていなければ現状を維持
            // If left click is not pressed, maintain the current state
            MouseCursorTooltip.Instance.Show(LocalizationKeys.Ui.Tooltip.PickUpLeftClick);
            return this;
        }
        
        private IMapObjectMiningState MiningProcess(MapObjectMiningControllerContext context)
        {
            var currentTarget = context.CurrentFocusTarget;
            var equippedItemId = context.LocalPlayerEquipment.SelectedItem.Id;

            // 無効装備ならフォーカス維持
            // Keep focus for invalid equipment
            if (!currentTarget.TryResolveUsableTool(equippedItemId, out var usableMiningTool))
            {
                ShowRecommendMiningTools(currentTarget.UsableToolItemIds);
                return this;
            }
            
            // クリックしていあない場合はフォーカスを維持
            // If not clicked, maintain focus
            if (!InputManager.Playable.ScreenLeftClick.GetKey)
            {
                MouseCursorTooltip.Instance.Show(LocalizationKeys.Ui.Tooltip.HoldToGet);
                return this;
            }
            
            // マイニング状態に遷移
            // Transition to mining state
            MouseCursorTooltip.Instance.Hide();
            return new MapObjectMiningMiningState(usableMiningTool, equippedItemId);

            #region Internal

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
