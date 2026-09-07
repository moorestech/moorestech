using System.Collections.Generic;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    ///     開いているブロック/列車インベントリの真データ。スロット数はサーバー応答のアイテム数で決まる
    ///     Authoritative data of the open block/train inventory; the slot count comes from the server response
    /// </summary>
    public class SubInventoryModel : ISubInventory
    {
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => SubInventory.Count;
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; }

        public SubInventoryModel(ISubInventoryIdentifier identifier)
        {
            ISubInventoryIdentifier = identifier;
        }

        public void SetItems(IReadOnlyList<IItemStack> items)
        {
            SubInventory.Clear();
            SubInventory.AddRange(items);
        }

        public void SetItem(int slot, IItemStack item)
        {
            if (SubInventory.Count <= slot)
            {
                Debug.LogError($"インベントリのサイズを超えています。item:{item} slot:{slot}");
                return;
            }

            SubInventory[slot] = item;
        }
    }
}
