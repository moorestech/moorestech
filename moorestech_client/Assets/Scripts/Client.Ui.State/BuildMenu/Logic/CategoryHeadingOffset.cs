using System;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// カテゴリ見出しの上端（スクロール内容の座標）
    /// A category heading's top in scroll-content coordinates
    /// </summary>
    public readonly struct CategoryHeadingOffset
    {
        public readonly Guid CategoryGuid;
        public readonly float Top;

        public CategoryHeadingOffset(Guid categoryGuid, float top)
        {
            CategoryGuid = categoryGuid;
            Top = top;
        }
    }
}
