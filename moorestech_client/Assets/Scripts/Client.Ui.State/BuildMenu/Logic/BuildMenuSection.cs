using System;
using System.Collections.Generic;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// 1 つのサブカテゴリの区画
    /// The section of one sub-category
    /// </summary>
    public sealed class BuildMenuSection
    {
        public Guid CategoryGuid { get; }
        public Guid SubCategoryGuid { get; }
        public IReadOnlyList<BuildMenuCatalogEntry> Entries { get; }

        public BuildMenuSection(Guid categoryGuid, Guid subCategoryGuid, IReadOnlyList<BuildMenuCatalogEntry> entries)
        {
            CategoryGuid = categoryGuid;
            SubCategoryGuid = subCategoryGuid;
            Entries = entries;
        }
    }
}
