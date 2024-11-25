using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class MachineBlockInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform machineInputItemParent;
        [SerializeField] private RectTransform machineOutputItemParent;
        [SerializeField] private TMP_Text machineBlockNameText;
        
        [SerializeField] private ProgressArrowView machineProgressArrow;
        
        private BlockGameObject _blockGameObject;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            _blockGameObject = blockGameObject;
            
            var itemList = new List<IItemStack>();
            var param = blockGameObject.BlockMasterElement.BlockParam;
            var inputCount = param switch
            {
                ElectricMachineBlockParam blockParam => blockParam.InputItemSlotCount, // TODO master interfaceブロックインベントリの整理箇所
                GearMachineBlockParam blockParam => blockParam.InputItemSlotCount,
                _ => 0
            };
            for (var i = 0; i < inputCount; i++)
            {
                var slotObject = Instantiate(itemSlotObjectPrefab, machineInputItemParent);
                _blockItemSlotObjects.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            var outputCount = param switch
            {
                ElectricMachineBlockParam blockParam => blockParam.OutputItemSlotCount, // TODO master interfaceブロックインベントリの整理箇所
                GearMachineBlockParam blockParam => blockParam.OutputItemSlotCount,
                _ => 0
            };
            for (var i = 0; i < outputCount; i++)
            {
                var slotObject = Instantiate(itemSlotObjectPrefab, machineOutputItemParent);
                _blockItemSlotObjects.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            machineBlockNameText.text = blockGameObject.BlockMasterElement.Name;
            UpdateItemList(itemList);
        }
        
        private void Update()
        {
            // ここが重かったら検討
            var commonProcessor = (CommonMachineBlockStateChangeProcessor)_blockGameObject.BlockStateChangeProcessors.FirstOrDefault(x => x as CommonMachineBlockStateChangeProcessor);
            if (commonProcessor == null) return;
            
            machineProgressArrow.SetProgress(commonProcessor.CurrentMachineState?.ProcessingRate ?? 0.0f);
        }
    }
}