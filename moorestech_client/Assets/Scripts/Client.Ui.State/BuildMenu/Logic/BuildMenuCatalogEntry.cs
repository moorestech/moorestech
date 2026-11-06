using System;
using System.Collections.Generic;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// 群分け・検索・不足判定が扱うエントリ。表示名は作る側が IPlacementTarget.DisplayName から入れる
    /// The entry that grouping, search and shortage work on; the producer fills the label from IPlacementTarget.DisplayName
    /// </summary>
    public sealed class BuildMenuCatalogEntry
    {
        // 設置対象の同一性は GUID 1 本（IPlacementTarget.Id）
        // Placement identity is a single GUID (IPlacementTarget.Id)
        public Guid TargetId { get; }
        public Guid CategoryGuid { get; }
        public Guid SubCategoryGuid { get; }
        public string DisplayLabel { get; }
        public IReadOnlyList<BuildMenuRequiredItem> RequiredItems { get; }

        // 素材を払わずに置ける局面か。素材の不足そのものは RequiredItems が持つ
        // Whether this placement skips paying; the material shortage itself stays in RequiredItems
        public bool PaymentWaived { get; }

        public BuildMenuCatalogEntry(Guid targetId, Guid categoryGuid, Guid subCategoryGuid, string displayLabel, IReadOnlyList<BuildMenuRequiredItem> requiredItems, bool paymentWaived)
        {
            TargetId = targetId;
            CategoryGuid = categoryGuid;
            SubCategoryGuid = subCategoryGuid;
            DisplayLabel = displayLabel;
            RequiredItems = requiredItems;
            PaymentWaived = paymentWaived;
        }
    }
}
