using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.MapModule;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningFocusState : IMapObjectMiningState
    {
        public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
        {
            // フォーカスが外れたのであればIdleに遷移
            // If the focus is lost, transition to Idle
            if (context.CurrentFocusMapObjectGameObject == null)
            {
                return new MapObjectMiningIdleState();
            }
            
            // MapObjectのマスターデータが取得できない場合はIdleに遷移
            // If the master data of MapObject cannot be obtained, transition to Idle
            var currentMapObjectMaster = context.CurrentFocusMapObjectGameObject.MapObjectMasterElement;
            if (currentMapObjectMaster == null)
            {
                return new MapObjectMiningIdleState();
            }
            var miningType = currentMapObjectMaster.MiningType;
            
            if (miningType == MapObjectMasterElement.MiningTypeConst.PickUp)
            {
                return PickUpProcess(context);
            }
            if (miningType == MapObjectMasterElement.MiningTypeConst.Mining)
            {
                return MiningProcess(currentMapObjectMaster, context);
            }
            
            throw new System.Exception("MiningType is not defined");
        }
        
        private IMapObjectMiningState PickUpProcess(MapObjectMiningControllerContext context)
        {
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown)
            {
                MouseCursorTooltip.Instance.Hide();
                return new MapObjectMiningMiningCompleteState(context.CurrentFocusMapObjectGameObject);
            }
            
            // 左クリックがされていなければ現状を維持
            // If left click is not pressed, maintain the current state
            MouseCursorTooltip.Instance.Show(LocalizationKeys.Ui.Tooltip.PickUpLeftClick);
            return this;
        }
        
        private IMapObjectMiningState MiningProcess(MapObjectMasterElement masterElement,MapObjectMiningControllerContext context)
        {
            var miningTools = ((MiningMiningParam)masterElement.MiningParam).MiningTools;
            var usableMiningTool = context.ResolveUsableTool(miningTools);

            // 無効装備ならフォーカス維持
            // Keep focus for invalid equipment
            if (usableMiningTool == null)
            {
                ShowRecommendMiningTools(miningTools);
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
            return new MapObjectMiningMiningState(usableMiningTool, context.LocalPlayerEquipment.SelectedItem.Id);

            #region Internal

            void ShowRecommendMiningTools(MiningToolsElement[] tools)
            {
                var localizedToolNames = new List<string>();
                foreach (var tool in tools)
                {
                    localizedToolNames.Add(Localize.GetContent(
                        ContentLocalizationKeys.ItemName(tool.ToolItemGuid)));
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
