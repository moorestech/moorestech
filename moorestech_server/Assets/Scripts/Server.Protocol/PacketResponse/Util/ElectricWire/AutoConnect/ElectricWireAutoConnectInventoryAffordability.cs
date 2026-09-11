using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 通常設置の所持判定。建設コストで消費予定の素材を予約として上乗せし、実在庫と突き合わせる
    /// Affordability for normal placement; adds construction-cost reservations on top and checks against the real inventory
    /// </summary>
    public class ElectricWireAutoConnectInventoryAffordability : IElectricWireAutoConnectAffordability
    {
        private readonly IReadOnlyList<(ItemId itemId, int count)> _reservedItems;
        private readonly IReadOnlyList<IItemStack> _inventoryItems;

        public ElectricWireAutoConnectInventoryAffordability(IReadOnlyList<(ItemId itemId, int count)> reservedItems, IReadOnlyList<IItemStack> inventoryItems)
        {
            _reservedItems = reservedItems;
            _inventoryItems = inventoryItems;
        }

        public bool CanAfford(IReadOnlyDictionary<ItemId, int> requiredByItem)
        {
            // 予約済みの数量を必要数へ上乗せし、全素材が所持数に収まるかを見る
            // Add reserved quantities to each requirement and check every material fits in the held count
            foreach (var (itemId, required) in requiredByItem)
            {
                if (CountHeld(itemId) < required + CountReserved(itemId)) return false;
            }
            return true;
        }

        private int CountReserved(ItemId itemId)
        {
            var total = 0;
            foreach (var reservedItem in _reservedItems)
            {
                if (reservedItem.itemId == itemId) total += reservedItem.count;
            }
            return total;
        }

        private int CountHeld(ItemId itemId)
        {
            var total = 0;
            foreach (var itemStack in _inventoryItems)
            {
                if (itemStack.Id == itemId) total += itemStack.Count;
            }
            return total;
        }
    }
}
