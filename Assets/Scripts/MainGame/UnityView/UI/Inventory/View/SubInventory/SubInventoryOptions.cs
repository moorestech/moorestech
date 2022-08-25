using System.Collections.Generic;

namespace MainGame.UnityView.UI.Inventory.View.SubInventory
{
    public class SubInventoryOptions
    {
        /// <summary>
        /// ダブルクリックでアイテムを集める時に、その対象から除外するスロットを指定する
        /// 例えばクラフトの結果スロットはアイテム収集の対象にはならない
        /// </summary>
        public List<int> WithoutCollectSlots = new();
    }
}