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
        
        /// <summary>
        ///     装備中アイテムに対応する採掘ツールを引く。採掘中の装備切替検知も同じ照合を使う
        ///     Resolves the mining tool matching the equipped item; mid-mining equipment-change detection reuses this
        /// </summary>
        public static MiningToolsElement ResolveUsableTool(MiningToolsElement[] miningTools, ItemId equippedItemId)
        {
            if (equippedItemId == ItemMaster.EmptyItemId) return null;

            var currentItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItemId).ItemGuid;
            foreach (var miningTool in miningTools)
            {
                if (miningTool.ToolItemGuid == currentItemGuid) return miningTool;
            }

            return null;
        }

        private IMapObjectMiningState MiningProcess(MapObjectMasterElement masterElement,MapObjectMiningControllerContext context)
        {
            // 今装備しているアイテムがマイニングツールとして登録されているかどうかをチェック
            // Check if the item you are currently equipping is registered as a mining tool
            var miningTools = ((MiningMiningParam)masterElement.MiningParam).MiningTools;
            var usableMiningTool = ResolveUsableTool(miningTools, context.LocalPlayerEquipment.SelectedItem.Id);

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
            return new MapObjectMiningMiningState(usableMiningTool);
        }
        
        
        private void ShowRecommendMiningTools(MiningToolsElement[] miningTools)
        {
            var result = new List<string>();
            
            foreach (var tool in miningTools)
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(tool.ToolItemGuid);
                result.Add(itemMaster.Name);
            }
            
            var text = "このアイテムが必要です:" + string.Join(", ",result);
            
            MouseCursorTooltip.Instance.Show(text, isLocalize: false);
        }
    }
}