using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
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
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly BeltReplaceRunBuilder _replaceRunBuilder;

        public BeltConveyorPlaceRunBuilder(BlockGameObjectDataStore blockGameObjectDataStore, CommonBlockPlaceDragState dragState)
        {
            _placePointCalculator = new BeltConveyorPlacePointCalculator(blockGameObjectDataStore);
            _dragState = dragState;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _replaceRunBuilder = new BeltReplaceRunBuilder(blockGameObjectDataStore);
        }

        // blockCauses/beltReasonsはPlaceInfo列と同添字で並走する原因列
        // blockCauses/beltReasons are cause columns indexed like the returned PlaceInfo list
        public List<PlaceInfo> Build(Vector3Int dragStartPoint, Vector3Int placePoint, BlockDirection blockDirection, BeltConveyorHoldingBlock holdingBlock, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)
        {
            // 軸決めはドラッグに属するためドラッグ状態へ委ねる
            // The axis belongs to the drag, so the drag state owns it
            var isStartDirectionZ = _dragState.ResolveDragAxisIsZ(dragStartPoint, placePoint);

            // 起点に既設ファミリーブロックがあれば張替え経路。空き地起点は従来の新規設置経路
            // A family block at the origin selects the replace run; an empty origin keeps the normal placement run
            if (BeltReplaceRunBuilder.TryResolveOrigin(_blockGameObjectDataStore, dragStartPoint, out var replaceOrigin))
            {
                return _replaceRunBuilder.Build(replaceOrigin, placePoint, isStartDirectionZ, holdingBlock, out blockCauses, out beltReasons);
            }

            // 坂選択中は一定勾配の専用経路のみ
            // A slope selection uses only the constant-grade path
            if (holdingBlock.SlopeGrade.HasValue)
            {
                return _placePointCalculator.CalculateSlopePoint(dragStartPoint, placePoint, isStartDirectionZ, blockDirection, holdingBlock, out blockCauses, out beltReasons);
            }

            var cellInfos = _placePointCalculator.CalculateStraightPoint(dragStartPoint, placePoint, isStartDirectionZ, blockDirection, holdingBlock.BlockMaster, out blockCauses, out beltReasons);

            // セル列へ直線・坂を1対1で割り当てる
            // Assign straight/slope blocks to cells one-to-one
            return BeltConveyorStraightCellBlockResolver.ResolveStraightRun(cellInfos, holdingBlock.BlockId, holdingBlock.RunUpBlockId, holdingBlock.RunDownBlockId, beltReasons);
        }
    }
}
