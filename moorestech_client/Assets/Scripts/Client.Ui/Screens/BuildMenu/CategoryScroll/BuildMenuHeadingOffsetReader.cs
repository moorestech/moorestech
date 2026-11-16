using System;
using System.Collections.Generic;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    public static class BuildMenuHeadingOffsetReader
    {
        // 表示中のカテゴリの見出しの上端をスクロール内容の座標で集める
        // Collect the visible categories' heading tops in scroll-content coordinates
        public static List<CategoryHeadingOffset> Read(IBuildMenuScrollViewport viewport, IReadOnlyList<Guid> categoryGuids)
        {
            var offsets = new List<CategoryHeadingOffset>();
            foreach (var categoryGuid in categoryGuids)
                if (viewport.TryGetHeadingTop(categoryGuid, out var top)) offsets.Add(new CategoryHeadingOffset(categoryGuid, top));
            return offsets;
        }
    }
}
