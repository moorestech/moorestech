using System;
using System.Collections.Generic;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// ビルドメニューのカテゴリ定義。サブカテゴリの並びが表示順の正（buildMenu マスタの配列順）
    /// A build-menu category definition; the sub-category order is the display order (the buildMenu master's array order)
    /// </summary>
    public sealed class BuildMenuCategory
    {
        public Guid CategoryGuid { get; }
        public IReadOnlyList<Guid> SubCategoryGuids { get; }

        public BuildMenuCategory(Guid categoryGuid, IReadOnlyList<Guid> subCategoryGuids)
        {
            CategoryGuid = categoryGuid;
            SubCategoryGuids = subCategoryGuids;
        }
    }
}
