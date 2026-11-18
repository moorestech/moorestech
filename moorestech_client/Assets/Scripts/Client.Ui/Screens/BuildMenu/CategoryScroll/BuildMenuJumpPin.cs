using System;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// ジャンプ中に固定する目標のカテゴリと到達位置
    /// The pinned jump target category and its arrival position
    /// </summary>
    internal readonly struct BuildMenuJumpPin
    {
        public readonly Guid CategoryGuid;
        public readonly float Top;

        public BuildMenuJumpPin(Guid categoryGuid, float top)
        {
            CategoryGuid = categoryGuid;
            Top = top;
        }
    }
}
