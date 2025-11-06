using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    /// <summary>
    /// インベントリを持つ基本的なブロックのクラスです。通常のインベントリを持つようなブロックであればこれを継承して実装してください。
    /// クラスの肥大化防止の為、専用処理を書かないといけなくなったときは<see cref="IBlockInventoryView"/>を実装した新たなクラスを作成してください。
    /// This is the class of the basic block with inventory. If you have a block that has a normal inventory, you can implement it by inheriting from this class.
    /// If you need to write dedicated processing to prevent class bloat, create a new class that implements <see cref="IBlockInventoryView"/>.
    /// </summary>
    public abstract class CommonBlockInventoryViewBase : MonoBehaviour, IBlockInventoryView
    {
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects => SubInventorySlotObjectsInternal;
        public int Count => SubInventorySlotObjectsInternal.Count;
        public List<IItemStack> SubInventory { get; } = new();
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; protected set; }
        
        /// <summary>
        /// インベントリとして機能させるために、作成したItemSlotViewは必ずここに入れてください。
        /// ここに登録されているリストはインベントリのスロットとしてみなされます。
        /// Make sure to put the created ItemSlotView here to make it function as an inventory.
        /// The list registered here is considered as the slots of the inventory.
        /// </summary>
        protected readonly List<ItemSlotView> SubInventorySlotObjectsInternal = new();
        
        public virtual void Initialize(BlockGameObject blockGameObject)
        {
            ISubInventoryIdentifier = new BlockInventorySubInventoryIdentifier(blockGameObject.BlockPosInfo.OriginalPos);
        }
        
        public void UpdateItemList(List<IItemStack> response)
        {
            SubInventory.Clear();
            SubInventory.AddRange(response);
        }
        public void UpdateInventorySlot(int slot, IItemStack item)
        {
            if (SubInventory.Count <= slot)
            {
                //TODO ログ基盤にいれる
                Debug.LogError($"インベントリのサイズを超えています。item:{item} slot:{slot}");
                return;
            }
            
            SubInventory[slot] = item;
        }
        public void DestroyUI()
        {
            Destroy(gameObject);
        }
    }
}