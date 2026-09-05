using System.Collections.Generic;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    /// プレイヤーのインベントリとは別に、ブロックや列車など「他のインベントリ」を表すインターフェース
    /// Represents an inventory other than the player's own, such as a block or train inventory
    /// </summary>
    public interface ISubInventory
    {
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; }
    }

    public static class ISubInventoryExtension
    {
        public static bool IsEnableSubInventory(this ISubInventory subInventory) => subInventory.Count > 0;
    }

    public class EmptySubInventory : ISubInventory
    {
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => 0;
        public ISubInventoryIdentifier ISubInventoryIdentifier => null;
    }
}
