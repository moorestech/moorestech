using System;
using System.Collections.Generic;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// カテゴリ定義（サブカテゴリ順が表示順）
    /// A category definition; sub-category order is display order
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
