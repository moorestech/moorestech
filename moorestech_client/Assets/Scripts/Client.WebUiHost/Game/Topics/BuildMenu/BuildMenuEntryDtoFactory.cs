using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.WebUiHost.Game.Icons;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.PlacementTarget;
using Mooresmaster.Model.BuildMenuModule;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// 解放済み設置対象を web 配信用 DTO へ変換する
    /// Converts the unlocked placement targets into web-delivery DTOs
    /// </summary>
    public static class BuildMenuEntryDtoFactory
    {
        // 解放判定はResolverが持つ唯一の供給点へ委ね、ここは変換だけを担う
        // Delegates the unlock decision to the resolver's single supply point; this file only converts
        public static List<BuildMenuEntryDto> CreateDtos(PlacementTargetResolver placementTargetResolver, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)
        {
            return CreateDtos(placementTargetResolver.CreateUnlockedTargets(), walletQuery, inventoryItems);
        }

        public static List<BuildMenuEntryDto> CreateDtos(IReadOnlyList<IPlacementTarget> targets, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)
        {
            var dtos = new List<BuildMenuEntryDto>();
            var categoryMaster = MasterHolder.BuildMenuCategoryMaster;

            // 所持集計は全エントリで共有
            // Share the held tally across all entries
            var heldByItem = ConstructionMaterialHeldCounts.Tally(inventoryItems);

            // デバッグ設定はpublish毎に1回解決
            // Resolve the debug flag once per publish
            var freeBlockPlacement = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);

            // 共有カタログの列挙順（ブロック→車両→接続ツール→BPコピー→BP）がそのまま表示順
            // The shared catalog's order (blocks, train cars, connect tools, blueprint copy, blueprints) is the display order
            // カテゴリ整合はマスタロード時に検証済み（block参照はBlockMasterUtil・非ブロックはentrySource必須定義）
            // Category consistency is validated at master load (block refs by BlockMasterUtil, non-blocks by required entrySource)
            foreach (var target in targets)
            {
                var (categoryGuid, subCategoryGuid) = ResolveCategoryPair(target);

                // 財布へは1エントリ1回だけ問い合わせ、設置数表示と支払い免除の両方をここから導く
                // Ask the wallet once per entry and derive both the set display and the payment waiver from it
                var block = ResolveBlockTarget(target);
                var walletStatus = block == null ? null : walletQuery.GetWalletStatus(block.BlockId);
                var paymentSkipped = freeBlockPlacement || (walletStatus != null && ConstructionWalletUtil.IsCoveredByWallet(walletStatus.Value.RemainingCount));

                dtos.Add(new BuildMenuEntryDto
                {
                    // 設置対象IDはGuid文字列1本。kindは表示・振る舞い用で識別子ではない
                    // The id is a single GUID string; kind is for display/behavior, not identity
                    Id = target.Id.ToString("D"),
                    Kind = ResolveKind(target.Kind),
                    Label = target.Kind == PlacementTargetKind.Blueprint ? target.DisplayName : null,
                    CategoryGuid = categoryGuid.ToString("D"),
                    SubCategoryGuid = subCategoryGuid.ToString("D"),
                    RequiredItems = BuildMenuMaterialAvailability.CreateRequiredItemDtos(target, paymentSkipped, heldByItem),
                    SetPlacement = ResolveSetPlacement(walletStatus),
                    IconUrl = ResolveIconUrl(target),
                });
            }
            return dtos;

            #region Internal

            // ブロックだけカテゴリをブロックマスタ自身が持ち、他はentrySource定義のサブカテゴリへ入る
            // Only blocks carry their category on the block master; the rest go to their entrySource-defined sub category
            (Guid categoryGuid, Guid subCategoryGuid) ResolveCategoryPair(IPlacementTarget target)
            {
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

            // 財布を持たないブロックと非ブロックはnull状態のまま配信でキーごと省略される
            // Blocks without a wallet and non-block kinds stay null, which omits the key on the wire
            BuildMenuSetPlacementDto ResolveSetPlacement(ConstructionWalletStatus? status)
            {
                if (status == null) return null;
                return new BuildMenuSetPlacementDto { PerCost = status.Value.PlacementsPerCost, Remaining = status.Value.RemainingCount };
            }

            #endregion
        }

        // ブロックかどうかの供給源はKindのenum一本に揃える
        // The single supply point for "is this a block" is the Kind enum
        private static BlockPlacementTarget ResolveBlockTarget(IPlacementTarget target)
        {
            return target.Kind == PlacementTargetKind.Block ? (BlockPlacementTarget)target : null;
        }

        public static List<BuildMenuCategoryDto> CreateCategoryDtos()
        {
            // buildMenuマスタcategoriesの配列順そのままが表示順の正
            // The array order of the buildMenu master's categories is the source of truth for display order
            return MasterHolder.BuildMenuCategoryMaster.Categories
                .Select(c => new BuildMenuCategoryDto
                {
                    CategoryGuid = c.CategoryGuid.ToString("D"),
                    SubCategoryGuids = c.SubCategories
                        .Select(s => s.SubCategoryGuid.ToString("D")).ToList(),
                }).ToList();
        }

        // 確定済みKindを網羅switchで文字列化する。ホットバートピックと共有するため公開
        // Stringifies the settled Kind via an exhaustive switch; public so the hotbar topic shares it
        public static string ResolveKind(PlacementTargetKind kind)
        {
            return kind switch
            {
                PlacementTargetKind.Block => "block",
                PlacementTargetKind.TrainCar => "trainCar",
                PlacementTargetKind.ConnectTool => "connectTool",
                PlacementTargetKind.BlueprintCopy => "blueprintCopy",
                PlacementTargetKind.Blueprint => "blueprint",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        // アイコンURL解決もホットバートピックと共有する唯一の解決点
        // The single resolution point for icon URLs, shared with the hotbar topic
        public static string ResolveIconUrl(IPlacementTarget target)
        {
            switch (target.Kind)
            {
                case PlacementTargetKind.Block:
                {
                    var block = (BlockPlacementTarget)target;
                    // block-icons はblock inventoryトピックのBlockIconと共有するため揮発BlockIdのまま（Guid化はplan Aのスコープ外）
                    // block-icons is shared with the block inventory topic's BlockIcon, so it stays volatile BlockId (GUID conversion is out of plan A's scope)
                    return $"{BlockIconSource.PathPrefixConst}{block.BlockId.AsPrimitive()}{IconEndpoint.PathSuffix}";
                }
                case PlacementTargetKind.TrainCar:
                {
                    var trainCar = (TrainCarPlacementTarget)target;
                    return $"{TrainCarIconSource.PathPrefixConst}{trainCar.TrainCarGuid}{IconEndpoint.PathSuffix}";
                }
                case PlacementTargetKind.ConnectTool:
                {
                    var connectTool = (ConnectToolPlacementTarget)target;
                    return $"{ConnectToolIconSource.PathPrefixConst}{connectTool.ConnectToolGuid}{IconEndpoint.PathSuffix}";
                }
                case PlacementTargetKind.BlueprintCopy:
                case PlacementTargetKind.Blueprint:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target.Kind), target.Kind, null);
            }
        }
    }
}
