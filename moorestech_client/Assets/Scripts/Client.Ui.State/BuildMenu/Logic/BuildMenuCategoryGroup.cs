using System;
using System.Collections.Generic;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// 1 本スクロールの群の単位（カテゴリ 1 つ分）
    /// A unit of the single scroll: one category
    /// </summary>
    public sealed class BuildMenuCategoryGroup
    {
        public Guid CategoryGuid { get; }
        public IReadOnlyList<BuildMenuSection> Sections { get; }

        public BuildMenuCategoryGroup(Guid categoryGuid, IReadOnlyList<BuildMenuSection> sections)
        {
            CategoryGuid = categoryGuid;
            Sections = sections;
        }
    }
}
