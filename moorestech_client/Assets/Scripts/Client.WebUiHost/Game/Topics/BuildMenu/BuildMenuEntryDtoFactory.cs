using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.UnlockState;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// WebBuildMenuEntryCatalog の合成結果を web 配信用 DTO へ変換する
    /// Converts the WebBuildMenuEntryCatalog composition into web-delivery DTOs
    /// </summary>
    public static class BuildMenuEntryDtoFactory
    {
        public static List<BuildMenuEntryDto> CreateDtos(IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary)
        {
            var dtos = new List<BuildMenuEntryDto>();
            // カテゴリ整合はマスタロード時に検証済み（block参照はBlockMasterUtil・非ブロックはentrySource必須定義）
            // Category consistency is validated at master load (block refs by BlockMasterUtil, non-blocks by required entrySource)
            foreach (var entry in WebBuildMenuEntryCatalog.CreateEntries(unlockState, blueprintLibrary))
            {
                dtos.Add(new BuildMenuEntryDto
                {
                    EntryType = GetEntryTypeName(entry.Target),
                    EntryKey = GetEntryKey(entry.Target),
                    Label = entry.Label,
                    Category = entry.Category,
                    SubCategory = entry.SubCategory,
                    RequiredItems = entry.RequiredItems.Select(r => new BuildMenuRequiredItemDto { ItemId = r.ItemId.AsPrimitive(), Count = r.Count }).ToList(),
                    IconUrl = CreateIconUrl(entry.Target),
                });
            }
            return dtos;
        }

        public static List<BuildMenuCategoryDto> CreateCategoryDtos()
        {
            // buildMenuマスタcategoriesの配列順そのままが表示順の正
            // The array order of the buildMenu master's categories is the source of truth for display order
            return MasterHolder.BuildMenuCategoryMaster.Categories
                .Select(c => new BuildMenuCategoryDto
                {
                    Name = c.Name,
                    SubCategories = c.SubCategories.Select(s => s.Name).ToList(),
                }).ToList();
        }

        // web契約の entryType 文字列（select アクションの照合と共有する）
        // The web-contract entryType string (shared with the select action's matching)
        public static string GetEntryTypeName(IPlacementTarget target)
        {
            return target switch
            {
                BlockPlacementTarget => "block",
                TrainCarPlacementTarget => "trainCar",
                ConnectToolPlacementTarget => "connectTool",
                BlueprintCopyToolPlacementTarget => "blueprintCopy",
                BlueprintPlacementTarget => "blueprint",
                _ => target.GetType().Name,
            };
        }

        // 種別ごとの安定キー（配列indexは再配信でずれるため使わない）
        // Stable key per type (array indices shift across republishes, so they are never used)
        public static string GetEntryKey(IPlacementTarget target)
        {
            return target switch
            {
                BlockPlacementTarget block => block.BlockId.AsPrimitive().ToString(),
                TrainCarPlacementTarget trainCar => trainCar.TrainCarGuid.ToString(),
                ConnectToolPlacementTarget connectTool => connectTool.ConnectToolGuid.ToString(),
                BlueprintPlacementTarget blueprint => blueprint.BlueprintName,
                _ => string.Empty,
            };
        }

        private static string CreateIconUrl(IPlacementTarget target)
        {
            switch (target)
            {
                case BlockPlacementTarget block:
                    return $"{BlockIconEndpoint.PathPrefix}{block.BlockId.AsPrimitive()}{BlockIconEndpoint.PathSuffix}";
                case TrainCarPlacementTarget trainCar:
                    return $"{TrainCarIconEndpoint.PathPrefix}{trainCar.TrainCarGuid}{TrainCarIconEndpoint.PathSuffix}";
                case ConnectToolPlacementTarget connectTool:
                    // 接続ツールのアイコンはconnectToolのimagePathから配信する
                    // The connect tool icon is served from the connectTool's imagePath
                    return $"{ConnectToolIconEndpoint.PathPrefix}{connectTool.ConnectToolGuid}{ConnectToolIconEndpoint.PathSuffix}";
                default:
                    return null;
            }
        }
    }
}
