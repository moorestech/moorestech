using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Core.Master;
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
            MouseCursorTooltip.Instance.Show("左クリックで取得", isLocalize: false);
            return this;
        }
        
        private IMapObjectMiningState MiningProcess(MapObjectMasterElement masterElement,MapObjectMiningControllerContext context)
        {
            // 今装備しているアイテムがマイニングツールとして登録されているかどうかをチェック
            // Check if the item you are currently equipping is registered as a mining tool
            var miningTools = ((MiningMiningParam)masterElement.MiningParam).MiningTools;
            var usableMiningTool = context.ResolveUsableTool(miningTools);

            // 未選択、またはマイニングツールとして登録されていない場合はフォーカスを維持
            // If nothing is selected, or it is not registered as a mining tool, maintain focus
            if (usableMiningTool == null)
            {
                ShowRecommendMiningTools(miningTools);
                return this;
            }
            
            // クリックしていあない場合はフォーカスを維持
            // If not clicked, maintain focus
            if (!InputManager.Playable.ScreenLeftClick.GetKey)
            {
                MouseCursorTooltip.Instance.Show("左クリック長押しで取得", isLocalize: false);
                return this;
            }
            
            // マイニング状態に遷移
            // Transition to mining state
            MouseCursorTooltip.Instance.Hide();
            return new MapObjectMiningMiningState(usableMiningTool, context.LocalPlayerEquipment.SelectedItem.Id);

            #region Internal

            void ShowRecommendMiningTools(MiningToolsElement[] tools)
            {
                var toolNames = new List<string>();
                foreach (var tool in tools)
                {
                    toolNames.Add(MasterHolder.ItemMaster.GetItemMaster(tool.ToolItemGuid).Name);
                }

                MouseCursorTooltip.Instance.Show("このアイテムが必要です:" + string.Join(", ", toolNames), isLocalize: false);
            }

            #endregion
        }
    }
}