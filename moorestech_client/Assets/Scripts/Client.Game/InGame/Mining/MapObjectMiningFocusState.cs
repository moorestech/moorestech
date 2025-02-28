using System.Collections.Generic;
using Client.Game.InGame.UI.Util;
using Client.Input;
using Core.Master;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.MapObjectsModule;

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
                MouseCursorExplainer.Instance.Hide();
                return new MapObjectMiningMiningCompleteState(context.CurrentFocusMapObjectGameObject, int.MaxValue);
            }
            
            // 左クリックがされていなければ現状を維持
            // If left click is not pressed, maintain the current state
            MouseCursorExplainer.Instance.Show("左クリックで取得", isLocalize: false);
            return this;
        }
        
        private IMapObjectMiningState MiningProcess(MapObjectMasterElement masterElement,MapObjectMiningControllerContext context)
        {
            // 今持っているアイテムがマイニングツールとして登録されているかどうかをチェック
            // Check if the item you are currently holding is registered as a mining tool
            var hotBarInventoryIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(context.HotBarView.SelectIndex);
            var inventoryItem = context.LocalPlayerInventory[hotBarInventoryIndex];
                
            
            // 何も選択していない場合はフォーカスを維持
            // If nothing is selected, maintain focus
            var miningTools = ((MiningMiningParam)masterElement.MiningParam).MiningTools;
            if (inventoryItem.Id == ItemMaster.EmptyItemId)
            {
                ShowRecommendMiningTools(miningTools);
                return this;
            }
            
            
            // マイニングツールとして登録されているかどうかをチェック
            // Check if it is registered as a mining tool
            MiningToolsElement usableMiningTool = null; 
            var currentItemGuid = MasterHolder.ItemMaster.GetItemMaster(inventoryItem.Id).ItemGuid;
            foreach (var miningTool in miningTools)
            {
                if (miningTool.ToolItemGuid != currentItemGuid) continue;
                
                usableMiningTool = miningTool;
                break;
            }
            
            // マイニングツールとして登録されていない場合はフォーカスを維持
            // If it is not registered as a mining tool, maintain focus
            if (usableMiningTool == null)
            {
                ShowRecommendMiningTools(miningTools);
                return this;
            }
            
            // クリックしていあない場合はフォーカスを維持
            // If not clicked, maintain focus
            if (!InputManager.Playable.ScreenLeftClick.GetKey)
            {
                MouseCursorExplainer.Instance.Show("左クリック長押しで取得", isLocalize: false);
                return this;
            }
            
            // マイニング状態に遷移
            // Transition to mining state
            MouseCursorExplainer.Instance.Hide();
            return new MapObjectMiningMiningState(usableMiningTool, context.PlayerObjectController);
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
            
            MouseCursorExplainer.Instance.Show(text, isLocalize: false);
        }
    }
}