using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class GeneratorBlockInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private RectTransform powerGeneratorFuelItemParent;
        [SerializeField] private TMP_Text blockName;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            
            blockName.text = blockGameObject.BlockMasterElement.Name;
            
            var itemList = new List<IItemStack>();
            var param = blockGameObject.BlockMasterElement.BlockParam;
            var generatorParam = (IFuelItemSlotParam)param;
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