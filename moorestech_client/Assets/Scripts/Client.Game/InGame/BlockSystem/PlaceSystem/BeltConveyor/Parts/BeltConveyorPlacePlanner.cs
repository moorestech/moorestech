using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 手持ちが直線か坂かで経路計算を選び、セル列を組み立てる
    /// Chooses the path calculation by whether the held block is straight or a slope, and builds the cell list
    /// </summary>
    public class BeltConveyorPlacePlanner
    {
        private readonly BeltConveyorPlacePointCalculator _blockPlacePointCalculator;

        public BeltConveyorPlacePlanner(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockPlacePointCalculator = new BeltConveyorPlacePointCalculator(blockGameObjectDataStore);
        }

        // blockCauses・beltReasonsは戻り値のセル列と同じ添字で並走する不可原因の列
        // blockCauses and beltReasons are block-cause columns indexed like the returned cell list
        public List<PlaceInfo> Plan(BeltConveyorPlaceRequest request, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)
        {
            // 坂選択中は一定勾配の専用経路。立体交差も坂の自動割り当ても通さない
            // A selected slope uses the constant-grade path: neither the overpass nor the auto slope assignment runs
            if (request.Family.TryGetSlopeDirection(request.HoldingBlockId, out var slopeDirection))
            {
                return _blockPlacePointCalculator.CalculateSlopePoint(request.DragStartPoint, request.PlacePoint, request.IsStartZDirection, request.BlockDirection, request.HoldingBlockMaster, slopeDirection, out blockCauses, out beltReasons);
            }

            var cellInfos = _blockPlacePointCalculator.CalculatePoint(request.DragStartPoint, request.PlacePoint, request.IsStartZDirection, request.BlockDirection, request.HoldingBlockMaster, out blockCauses, out beltReasons);

            // セル列へ直線・坂ブロックを1対1で割り当てる（坂欠落はベルト固有理由の列へ書き戻される）
            // Assign straight and slope blocks to cells one-to-one (a missing slope is written back into the belt reason column)
            return BeltConveyorCellBlockResolver.Resolve(cellInfos, request.Family, beltReasons);
        }
    }

    /// <summary>
    /// 1フレーム分の経路計算入力
    /// One frame's worth of path calculation input
    /// </summary>
    public class BeltConveyorPlaceRequest
    {
        public readonly Vector3Int DragStartPoint;
        public readonly Vector3Int PlacePoint;
        public readonly bool IsStartZDirection;
        public readonly BlockDirection BlockDirection;
        public readonly BeltConveyorFamily Family;
        public readonly BlockId HoldingBlockId;
        public readonly BlockMasterElement HoldingBlockMaster;

        public BeltConveyorPlaceRequest(Vector3Int dragStartPoint, Vector3Int placePoint, bool isStartZDirection, BlockDirection blockDirection, BeltConveyorFamily family, BlockId holdingBlockId, BlockMasterElement holdingBlockMaster)
        {
            DragStartPoint = dragStartPoint;
            PlacePoint = placePoint;
            IsStartZDirection = isStartZDirection;
            BlockDirection = blockDirection;
            Family = family;
            HoldingBlockId = holdingBlockId;
            HoldingBlockMaster = holdingBlockMaster;
        }
    }
}
