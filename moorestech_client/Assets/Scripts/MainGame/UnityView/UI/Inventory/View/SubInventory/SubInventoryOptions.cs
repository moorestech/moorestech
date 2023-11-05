using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.View.SubInventory
{
    public class SubInventoryOptions
    {
        public Vector2Int BlockPosition;

        /// <summary>
        ///     ブロックかどうかのフラグと、そのブロックの位置
        /// </summary>
        public bool IsBlock = false;

        /// <summary>
        ///     ダブルクリックでアイテムを集める時に、その対象から除外するスロットを指定する
        ///     例えばクラフトの結果スロットはアイテム収集の対象にはならない
        /// </summary>
        public List<int> WithoutCollectSlots = new();
    }
}