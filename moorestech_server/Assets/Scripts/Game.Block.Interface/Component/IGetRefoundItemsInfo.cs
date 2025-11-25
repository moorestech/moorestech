using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Block.Interface.Component
{
    // ブロック削除時に返却すべきアイテム情報を取得するインターフェース
    // Interface for retrieving refundable item information when a block is removed
    public interface IGetRefoundItemsInfo : IBlockComponent
    {
        // 返却すべきアイテムのリストを取得する
        // Get list of items that should be refunded
        IReadOnlyList<IItemStack> GetRefundItems();
    }
}


