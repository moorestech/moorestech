using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// web建設メニュー1エントリの分類・表示情報
    /// A single web build-menu entry with its classification and display info
    /// </summary>
    public readonly struct WebBuildMenuEntry
    {
        public readonly IPlacementTarget Target;
        public readonly string Label;
        public readonly string Category;
        public readonly string SubCategory;
        public readonly IReadOnlyList<RequiredItem> RequiredItems;

        public WebBuildMenuEntry(IPlacementTarget target, string label, string category, string subCategory, IReadOnlyList<RequiredItem> requiredItems)
        {
            Target = target;
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
