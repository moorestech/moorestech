using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class MinerBlockInventoryView : CommonBlockInventoryViewBase 
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private ItemSlotObject minerResourceSlot;
        [SerializeField] private RectTransform minerResultsParent;
        
        [SerializeField] private ProgressArrowView minerProgressArrow;
        
        private BlockGameObject _blockGameObject;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            _blockGameObject = blockGameObject;
            
            var itemList = new List<IItemStack>();
            var param = blockGameObject.BlockMasterElement.BlockParam;
            var outputCount = param switch
            {
                ElectricMinerBlockParam blockParam => blockParam.OutputItemSlotCount, // TODO master interfaceブロックインベントリの整理箇所
                GearMinerBlockParam blockParam => blockParam.OutputItemSlotCount,
                _ => 0
            };
            
            for (var i = 0; i < outputCount; i++)
            {
                var slotObject = Instantiate(itemSlotObjectPrefab, minerResultsParent);
                SubInventorySlotObjectsInternal.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            UpdateItemList(itemList);
        }
        
        private void Update()
        {
            // ここが重かったら検討
            var commonProcessor = (CommonMachineBlockStateChangeProcessor)_blockGameObject.BlockStateChangeProcessors.FirstOrDefault(x => x as CommonMachineBlockStateChangeProcessor);
            if (commonProcessor == null) return;
            
            minerProgressArrow.SetProgress(commonProcessor.CurrentMachineState?.ProcessingRate ?? 0.0f);
        }
    }
}