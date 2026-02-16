using System.Linq;
using Core.Item.Interface;
using Core.Master;

namespace Game.Train.Unit.Containers
{
    public class ItemTrainCarContainer : ITrainCarContainer
    {
        private readonly IItemStack[] _inventoryItems;

        public ItemTrainCarContainer(IItemStack[] inventoryItems)
        {
            _inventoryItems = inventoryItems;
        }
        
        public int GetWeight()
        {
            return TrainMotionParameters.WEIGHT_PER_SLOT * _inventoryItems.Length;
        }

        public bool IsFull()
        {
            return _inventoryItems.All(stack => stack.Id != ItemMaster.EmptyItemId && stack.Count >= MasterHolder.ItemMaster.GetItemMaster(stack.Id).MaxStack);
        }
        
        public bool IsEmpty()
        {
            return _inventoryItems.All(stack => stack.Id == ItemMaster.EmptyItemId || stack.Count == 0);
        }
    }
}