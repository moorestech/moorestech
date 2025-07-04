using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class GeneratorBlockInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private RectTransform powerGeneratorFuelItemParent;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            
            
            var itemList = new List<IItemStack>();
            var param = blockGameObject.BlockMasterElement.BlockParam;
            var generatorParam = (ElectricGeneratorBlockParam)param;
            for (var i = 0; i < generatorParam.FuelItemSlotCount; i++)
            {
                var slotObject = Instantiate(ItemSlotView.Prefab, powerGeneratorFuelItemParent);
                SubInventorySlotObjectsInternal.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            UpdateItemList(itemList);
        }
    }
}