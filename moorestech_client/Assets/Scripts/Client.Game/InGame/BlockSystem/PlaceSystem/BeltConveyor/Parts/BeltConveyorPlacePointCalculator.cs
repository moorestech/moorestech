using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// コンベア専用の設置点計算（1マス刻み・カーブ・傾斜・立体交差を常時有効）
    /// Conveyor-only placement-point calculation (grid-step, curves, slopes, overpass always enabled)
    /// </summary>
    public class BeltConveyorPlacePointCalculator
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        public BeltConveyorPlacePointCalculator(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        // blockCauses・beltReasonsはPlaceInfo列と同じ添字で並走する不可原因の列（共有原因とベルト固有理由を分けて渡す）
        // blockCauses and beltReasons are block-cause columns indexed like the PlaceInfo list, separating shared causes from belt-specific reasons
        public List<PlaceInfo> CalculatePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement straightBlockMaster, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)
        {
            return CalculatePoint(startPoint, endPoint, isStartDirectionZ, blockDirection, straightBlockMaster, IsNotExistBlock, IsOccupied, out blockCauses, out beltReasons);
        }

        public static List<PlaceInfo> CalculatePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement straightBlockMaster, Func<PlaceInfo, BlockMasterElement, bool> isNotExistBlock, Func<Vector3Int, bool> isOccupied, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)
        {
            var (placeInfos, startToCornerDistance) = BeltConveyorPathBuilder.Build(startPoint, endPoint, isStartDirectionZ, blockDirection);

            // 障害物を自動で跨ぐ立体交差プロファイルを後段で重ねる
            // Layer the auto-overpass profile that steps over obstacles
            var overpassBlocked = new ConveyorOverpassRaiser().Raise(placeInfos, startToCornerDistance, isOccupied);

            blockCauses = new List<PlacementBlockCause>(placeInfos.Count);
            beltReasons = new List<BeltConveyorPlacementBlockReason>(placeInfos.Count);

            // Raiserが立体交差不能で立てた設置不可フラグを残したまま、占有判定を重ねる
            // Keep the infeasibility flag the Raiser set for an impossible overpass, then AND in occupancy.
            for (var i = 0; i < placeInfos.Count; i++)
            {
                blockCauses.Add(PlacementBlockCause.None);
                beltReasons.Add(overpassBlocked[i] ? BeltConveyorPlacementBlockReason.ImpossibleOverpass : BeltConveyorPlacementBlockReason.None);

                var info = placeInfos[i];
                if (!info.Placeable || isNotExistBlock(info, straightBlockMaster)) continue;

                info.Placeable = false;
                blockCauses[i] = PlacementBlockCause.ExistingBlock;
            }

            return placeInfos;
        }

        // 坂選択時の設置点計算（一定勾配・立体交差なし・全セル同一ブロック）
        // Placement-point calculation while a slope is selected (constant grade, no overpass, one block for every cell)
        public List<PlaceInfo> CalculateSlopePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement holdingBlockMaster, BlockVerticalDirection slopeDirection, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(startPoint, endPoint, isStartDirectionZ, blockDirection, slopeDirection);
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);

            blockCauses = new List<PlacementBlockCause>(placeInfos.Count);
            beltReasons = new List<BeltConveyorPlacementBlockReason>(placeInfos.Count);

            // 坂は立体交差も坂欠落も起こらないためベルト固有理由は立たない
            // A slope run raises neither an overpass nor a missing-slope reason, so the belt column stays None
            for (var i = 0; i < placeInfos.Count; i++)
            {
                var info = placeInfos[i];
                info.BlockId = holdingBlockId;
                blockCauses.Add(PlacementBlockCause.None);
                beltReasons.Add(BeltConveyorPlacementBlockReason.None);

                if (IsNotExistBlock(info, holdingBlockMaster)) continue;

                info.Placeable = false;
                blockCauses[i] = PlacementBlockCause.ExistingBlock;
            }

            return placeInfos;
        }

        // 直線ブロックの1セル範囲で既存ブロックとの重なりを判定する
        // Detect overlap in the straight block's single-cell area
        private bool IsNotExistBlock(PlaceInfo placeInfo, BlockMasterElement straightBlockMaster)
        {
            var previewPositionInfo = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, straightBlockMaster.BlockSize);
            return !_blockGameObjectDataStore.IsOverlapPositionInfo(previewPositionInfo);
        }

        // 1×1×1セルに既存ブロックが存在するか（障害物スキャン用）
        // Whether a 1x1x1 cell is occupied by an existing block (used by obstacle scanning).
        private bool IsOccupied(Vector3Int cell)
        {
            var positionInfo = new BlockPositionInfo(cell, BlockDirection.North, Vector3Int.one);
            return _blockGameObjectDataStore.IsOverlapPositionInfo(positionInfo);
        }
    }
}
