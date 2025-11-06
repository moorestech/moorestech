using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    /// プレイヤーのインベントリとは別に、ブロックや列車など「他のインベントリ」を表すインターフェース
    ///
    /// TODO この辺はリファクタリングとかして整理して、ドキュメント描きたいなぁ、、
    /// </summary>
    public interface ISubInventory
    {
        /// <summary>
        /// データ上での本当のサブインベントリのデータ。
        /// このように書くのは、アイテムを持った後の右クリック長押しでアイテムを分割できる機能において、実際のデータとUI上のデータが異なる場合があるため、真のデータとしてこれを保持しておく必要がある。
        /// </summary>
        public List<IItemStack> SubInventory { get; }
        
        /// <summary>
        /// サブインベントリのUIオブジェクト。上記の理由から、UIでの見た目と実データが異なることがある。そのため、UI側だけ表示を書き換えられるようにするために、このプロパティを用意している。
        /// </summary>
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; }
        
        /// <summary>
        /// SubInventoryのスロットの個数
        /// </summary>
        public int Count { get; }
        
        /// <summary>
        /// サブインベントリの識別子
        /// </summary>
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; }
    }
    
    public static class ISubInventoryExtension
    {
        public static bool IsEnableSubInventory(this ISubInventory subInventory) => subInventory.Count > 0;
    }
    
    public class EmptySubInventory : ISubInventory
    {
        public EmptySubInventory()
        {
            Count = 0;
            SubInventorySlotObjects = new List<ItemSlotView>();
            SubInventory = new List<IItemStack>();
            ISubInventoryIdentifier = null;
        }

        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; }
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; }
    }
}