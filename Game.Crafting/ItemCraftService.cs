using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;

namespace Game.Crafting
{
    public class ItemCraftService : IItemCraftService
    {
        private readonly IIsCreatableJudgementService _isCreatableJudgementService;
        private readonly IOpenableInventory _grabInventoryData;
        private readonly ICraftingOpenableInventory _craftingOpenableInventory;

        public ItemCraftService(IIsCreatableJudgementService isCreatableJudgementService, IOpenableInventory grabInventoryData, ICraftingOpenableInventory craftingOpenableInventory)
        {
            _isCreatableJudgementService = isCreatableJudgementService;
            _grabInventoryData = grabInventoryData;
            _craftingOpenableInventory = craftingOpenableInventory;
        }

        public void NormalCraft()
        {
            //クラフトが可能なアイテムの配置かチェック
            //クラフト結果のアイテムを持ちスロットに追加可能か判定
            if (!IsCreatable()) return;
            
            //クラフト結果のアイテムを取得しておく
            var result = _isCreatableJudgementService.GetResult(_craftingOpenableInventory.Items);
            if (!_grabInventoryData.GetItem(0).IsAllowedToAdd(result)) return;

            //クラフトしたアイテムを消費する
            ConsumptionCraftItem(1, _craftingOpenableInventory.Items);
            
            //元のクラフト結果のアイテムを足したアイテムを持ちインベントリに追加
            var outputSlotItem = _grabInventoryData.GetItem(0);
            var addedOutputSlot = outputSlotItem.AddItem(result).ProcessResultItemStack;
            _grabInventoryData.SetItem(0, addedOutputSlot);
        }

        private bool IsCreatable() { return _isCreatableJudgementService.IsCreatable(_craftingOpenableInventory.Items); }
        
        
        private void ConsumptionCraftItem(int itemCount,IReadOnlyList<IItemStack> craftingItems)
        {
            for (int i = 0; i < itemCount; i++)
            {
                var craftConfig = _isCreatableJudgementService.GetCraftingConfigData(craftingItems);
                for (int j = 0; j < PlayerInventoryConst.CraftingSlotSize; j++)
                {
                    //クラフトしたアイテムを消費する
                    var subItem = _craftingOpenableInventory.Items[j].SubItem(craftConfig.Items[j].Count);
                    //インベントリにセット
                    _craftingOpenableInventory.SetItem(j, subItem);
                }
            }
        }
    }
}