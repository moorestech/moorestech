using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ドラッグ範囲からベルト列のセルを組み立てる
    /// Builds the belt run's cells from the drag range
    /// </summary>
    public class BeltConveyorPlaceRunBuilder
    {
        private readonly BeltConveyorPlacePointCalculator _placePointCalculator;
        private readonly CommonBlockPlaceDragState _dragState;

        public BeltConveyorPlaceRunBuilder(BlockGameObjectDataStore blockGameObjectDataStore, CommonBlockPlaceDragState dragState, IConveyorTerrainFloor terrainFloor)
        {
            _placePointCalculator = new BeltConveyorPlacePointCalculator(blockGameObjectDataStore, terrainFloor);
            _dragState = dragState;
        }

        // 原因列はPlaceInfoと同添字、断念フラグは列全体に対応する
        // Cause columns match PlaceInfo indices; abandonment applies to the entire run
        public List<PlaceInfo> Build(Vector3Int dragStartPoint, Vector3Int placePoint, BlockDirection blockDirection, BeltConveyorHoldingBlock holdingBlock, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons, out bool terrainClimbAbandoned)
        {
            // 軸決めはドラッグに属するためドラッグ状態へ委ねる
            // The axis belongs to the drag, so the drag state owns it
            var isStartDirectionZ = _dragState.ResolveDragAxisIsZ(dragStartPoint, placePoint);

            // 坂選択中は地形を参照せず一定勾配の経路を使う
            // A slope selection uses a constant-grade path without terrain queries
            if (holdingBlock.SlopeGrade.HasValue)
            {
                terrainClimbAbandoned = false;
                return _placePointCalculator.CalculateSlopePoint(dragStartPoint, placePoint, isStartDirectionZ, blockDirection, holdingBlock, out blockCauses, out beltReasons);
            }

            var cellInfos = _placePointCalculator.CalculateStraightPoint(dragStartPoint, placePoint, isStartDirectionZ, blockDirection, holdingBlock.BlockMaster, out blockCauses, out beltReasons, out terrainClimbAbandoned);

            // セル列へ直線・坂を1対1で割り当てる
            // Assign straight/slope blocks to cells one-to-one
            return BeltConveyorStraightCellBlockResolver.ResolveStraightRun(cellInfos, holdingBlock.Family, beltReasons);
        }
    }
}
