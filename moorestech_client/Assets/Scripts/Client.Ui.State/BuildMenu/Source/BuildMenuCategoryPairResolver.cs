using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.PlacementTarget;
using Mooresmaster.Model.BuildMenuModule;

namespace Client.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// 配置対象 → カテゴリ対（Web の橋 BuildMenuEntryDtoFactory.ResolveCategoryPair の写し）
    /// Placement target to category pair (port of the Web bridge BuildMenuEntryDtoFactory.ResolveCategoryPair)
    /// </summary>
    public static class BuildMenuCategoryPairResolver
    {
        public static (Guid categoryGuid, Guid subCategoryGuid) Resolve(IPlacementTarget target)
        {
            var categoryMaster = MasterHolder.BuildMenuCategoryMaster;

            // ブロックはマスタのカテゴリ名、それ以外は入り口の種別でサブカテゴリを引く
            // Blocks use the master category names; other kinds look up the sub-category by entry source
            if (target is BlockPlacementTarget block)
            {
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
                return categoryMaster.GetGuidPair(blockMaster.Category, blockMaster.SubCategory);
            }
            return categoryMaster.GetPairByEntrySource(target.Kind switch
            {
                PlacementTargetKind.TrainCar => BuildMenuSubCategoryElement.EntrySourceConst.trainCars,
                PlacementTargetKind.ConnectTool => BuildMenuSubCategoryElement.EntrySourceConst.connectTools,
                PlacementTargetKind.BlueprintCopy => BuildMenuSubCategoryElement.EntrySourceConst.blueprintCopyTool,
                PlacementTargetKind.Blueprint => BuildMenuSubCategoryElement.EntrySourceConst.savedBlueprints,
                _ => throw new ArgumentOutOfRangeException(nameof(target.Kind), target.Kind, null),
            });
        }
    }
}
