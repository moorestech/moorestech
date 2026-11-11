using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;
using Client.Ui.State.PlacementTarget;
using Game.PlacementTarget;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// ビルドメニューのエントリ 1 件。群分け・検索・不足判定は Catalog（L0-s3 の型）で行う
    /// One build-menu entry; grouping, search and shortage run on Catalog (the L0-s3 type)
    /// </summary>
    public sealed class BuildMenuEntryViewModel
    {
        public BuildMenuCatalogEntry Catalog { get; }
        public string KindName { get; }
        public bool CanDeleteBlueprint { get; }
        public IReadOnlyList<BuildMenuMaterialLine> Materials { get; }
        public PlacementTargetIcon Icon { get; }
        public BuildMenuSetPlacement SetPlacement { get; }

        public BuildMenuEntryViewModel(Guid targetId, PlacementTargetKind kind, Guid categoryGuid, Guid subCategoryGuid, string displayLabel, IReadOnlyList<BuildMenuMaterialRow> materials, bool paymentWaived, PlacementTargetIcon icon, BuildMenuSetPlacement setPlacement)
        {
            // 必要素材は 1 本の列から Catalog へ写し、二重に持たない
            // Required items are copied from a single list into Catalog, never held twice
            Catalog = new BuildMenuCatalogEntry(targetId, categoryGuid, subCategoryGuid, displayLabel, materials.Select(row => row.Item).ToList(), paymentWaived);
            // Screens が Game.PlacementTarget を見ずに済むよう、testid の種別名と BP 削除の可否をここで決める
            // Decide the testid kind name and blueprint deletability here so Screens never look at Game.PlacementTarget
            KindName = BuildMenuEntryKindName.Of(kind);
            CanDeleteBlueprint = kind == PlacementTargetKind.Blueprint;
            Materials = materials.Select(row => new BuildMenuMaterialLine(row, BuildMenuShortage.IsItemInsufficient(Catalog, row.Item))).ToList();
            Icon = icon;
            SetPlacement = setPlacement;
        }
    }
}
