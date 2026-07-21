using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Mod.Texture;
using Core.Master;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// 建設メニュー1エントリの表示・分類情報。tooltip文字列は持たず構造化データのみ持つ
    /// A single build menu entry. Holds structured data instead of a preformatted tooltip string
    /// </summary>
    public readonly struct BuildMenuEntry
    {
        public readonly IPlacementTarget Target;

        // アイコン無し（BP等）はnullでテキスト表示になる
        // Null icon (e.g. blueprints) renders as a text-only slot
        public readonly ItemViewData IconView;
        public readonly string Label;
        public readonly string Category;
        public readonly string SubCategory;
        public readonly IReadOnlyList<RequiredItem> RequiredItems;

        public BuildMenuEntry(IPlacementTarget target, ItemViewData iconView, string label, string category, string subCategory, IReadOnlyList<RequiredItem> requiredItems)
        {
            Target = target;
            IconView = iconView;
            Label = label;
            Category = category;
            SubCategory = subCategory;
            RequiredItems = requiredItems;
        }

        public readonly struct RequiredItem
        {
            public readonly ItemId ItemId;
            public readonly int Count;

            public RequiredItem(ItemId itemId, int count)
            {
                ItemId = itemId;
                Count = count;
            }
        }
    }
}
