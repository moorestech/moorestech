using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.PlacementTarget;
using Game.UnlockState;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// WebBuildMenuEntryCatalog の合成結果を web 配信用 DTO へ変換する
    /// Converts the WebBuildMenuEntryCatalog composition into web-delivery DTOs
    /// </summary>
    public static class BuildMenuEntryDtoFactory
    {
        public static List<BuildMenuEntryDto> CreateDtos(IGameUnlockStateData unlockState, PlacementTargetCatalog placementTargetCatalog)
        {
            var dtos = new List<BuildMenuEntryDto>();
            // カテゴリ整合はマスタロード時に検証済み（block参照はBlockMasterUtil・非ブロックはentrySource必須定義）
            // Category consistency is validated at master load (block refs by BlockMasterUtil, non-blocks by required entrySource)
            foreach (var entry in WebBuildMenuEntryCatalog.CreateEntries(unlockState, placementTargetCatalog))
            {
                dtos.Add(new BuildMenuEntryDto
                {
                    Id = GetId(entry.Target),
                    Kind = GetKind(entry.Target.Kind),
                    Label = entry.Label,
                    Category = entry.Category,
                    SubCategory = entry.SubCategory,
                    RequiredItems = entry.RequiredItems.Select(r => new BuildMenuRequiredItemDto { ItemId = r.ItemId.AsPrimitive(), Count = r.Count }).ToList(),
                    IconUrl = CreateIconUrl(entry.Target),
                });
            }
            return dtos;

            #region Internal

            // 設置対象IDはGuid文字列1本。kindは表示・振る舞い用で識別子ではない
            // The id is a single GUID string; kind is for display/behavior, not identity
            string GetId(IPlacementTarget target)
            {
                return target.Id.ToString();
            }

            // PlacementTargetCatalogが既に確定させたKind enumを網羅switchで文字列化する（型switchの二重分類・未知値文字列漏れを禁止）
            // Stringify the Kind enum the PlacementTargetCatalog already determined via an exhaustive switch (no duplicate type-pattern classification, no unknown-value string leak)
            string GetKind(PlacementTargetKind kind)
            {
                return kind switch
                {
                    PlacementTargetKind.Block => "block",
                    PlacementTargetKind.TrainCar => "trainCar",
                    PlacementTargetKind.ConnectTool => "connectTool",
                    PlacementTargetKind.BuildTool => "buildTool",
                    PlacementTargetKind.Blueprint => "blueprint",
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
                };
            }

            string CreateIconUrl(IPlacementTarget target)
            {
                switch (target.Kind)
                {
                    case PlacementTargetKind.Block:
                    {
                        var block = (BlockPlacementTarget)target;
                        // block-icons はblock inventoryトピックのBlockIconと共有するため揮発BlockIdのまま（Guid化はplan Aのスコープ外）
                        // block-icons is shared with the block inventory topic's BlockIcon, so it stays volatile BlockId (GUID conversion is out of plan A's scope)
                        return $"{BlockIconEndpoint.PathPrefix}{block.BlockId.AsPrimitive()}{BlockIconEndpoint.PathSuffix}";
                    }
                    case PlacementTargetKind.TrainCar:
                    {
                        var trainCar = (TrainCarPlacementTarget)target;
                        return $"{TrainCarIconEndpoint.PathPrefix}{trainCar.TrainCarGuid}{TrainCarIconEndpoint.PathSuffix}";
                    }
                    case PlacementTargetKind.ConnectTool:
                    {
                        var connectTool = (ConnectToolPlacementTarget)target;
                        // 接続ツールのアイコンはconnectToolのimagePathから配信する
                        // The connect tool icon is served from the connectTool's imagePath
                        return $"{ConnectToolIconEndpoint.PathPrefix}{connectTool.ConnectToolGuid}{ConnectToolIconEndpoint.PathSuffix}";
                    }
                    case PlacementTargetKind.BuildTool:
                    case PlacementTargetKind.Blueprint:
                        return null;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(target.Kind), target.Kind, null);
                }
            }

            #endregion
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
    }
}
