using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.BuildMenuModule;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// web建設メニュー1エントリの分類・表示情報
    /// A single web build-menu entry with its classification and display info
    /// </summary>
    public readonly struct WebBuildMenuEntry
    {
        public readonly IPlacementTarget Target;
        public readonly string Label;
        public readonly Guid CategoryGuid;
        public readonly Guid SubCategoryGuid;
        public readonly IReadOnlyList<RequiredItem> RequiredItems;

        private WebBuildMenuEntry(
            IPlacementTarget target,
            string label,
            Guid categoryGuid,
            Guid subCategoryGuid,
            IReadOnlyList<RequiredItem> requiredItems)
        {
            Target = target;
            Label = label;
            CategoryGuid = categoryGuid;
            SubCategoryGuid = subCategoryGuid;
            RequiredItems = requiredItems;
        }

        public static WebBuildMenuEntry CreateBlock(
            BlockId blockId,
            BlockMasterElement blockMaster,
            IReadOnlyList<RequiredItem> requiredItems)
        {
            var (categoryGuid, subCategoryGuid) =
                MasterHolder.BuildMenuCategoryMaster.GetGuidPair(blockMaster.Category, blockMaster.SubCategory);
            return new WebBuildMenuEntry(
                new BlockPlacementTarget(blockId, null),
                null,
                categoryGuid,
                subCategoryGuid,
                requiredItems);
        }

        public static WebBuildMenuEntry CreateTrainCar(
            Guid trainCarGuid,
            IReadOnlyList<RequiredItem> requiredItems)
        {
            var (categoryGuid, subCategoryGuid) =
                MasterHolder.BuildMenuCategoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.trainCars);
            return new WebBuildMenuEntry(
                new TrainCarPlacementTarget(trainCarGuid),
                null,
                categoryGuid,
                subCategoryGuid,
                requiredItems);
        }

        public static WebBuildMenuEntry CreateConnectTool(
            Guid connectToolGuid,
            IReadOnlyList<RequiredItem> requiredItems)
        {
            var (categoryGuid, subCategoryGuid) =
                MasterHolder.BuildMenuCategoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.connectTools);
            return new WebBuildMenuEntry(
                new ConnectToolPlacementTarget(connectToolGuid),
                null,
                categoryGuid,
                subCategoryGuid,
                requiredItems);
        }

        public static WebBuildMenuEntry CreateBlueprintCopy(IReadOnlyList<RequiredItem> requiredItems)
        {
            var (categoryGuid, subCategoryGuid) =
                MasterHolder.BuildMenuCategoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.blueprintCopyTool);
            return new WebBuildMenuEntry(
                new BlueprintCopyToolPlacementTarget(),
                null,
                categoryGuid,
                subCategoryGuid,
                requiredItems);
        }

        public static WebBuildMenuEntry CreateBlueprint(
            string blueprintName,
            IReadOnlyList<RequiredItem> requiredItems)
        {
            var (categoryGuid, subCategoryGuid) =
                MasterHolder.BuildMenuCategoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.savedBlueprints);
            return new WebBuildMenuEntry(
                new BlueprintPlacementTarget(blueprintName),
                blueprintName,
                categoryGuid,
                subCategoryGuid,
                requiredItems);
        }

        public readonly struct RequiredItem
        {
            public readonly ItemId ItemId;
            public readonly int Count;

            public RequiredItem(ItemId itemId, int count)
            {
                ItemId = itemId;
                Count = count;
            }
        }
    }
}
