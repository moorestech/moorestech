using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ドラッグ範囲からベルト列の設置セルを組み立て、列の軸（X先行かZ先行か）を保持する
    /// Builds the belt run's placement cells from the drag range and keeps the run's axis (X-first or Z-first)
    /// </summary>
    public class BeltConveyorPlaceRunBuilder
    {
        private readonly BeltConveyorPlacePointCalculator _placePointCalculator;

        private bool? _isStartZDirection;

        public BeltConveyorPlaceRunBuilder(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _placePointCalculator = new BeltConveyorPlacePointCalculator(blockGameObjectDataStore);
        }

        // 軸決めはドラッグに属するため、ドラッグを畳むときに一緒に捨てる
        // The axis choice belongs to the drag, so it is dropped together when the drag folds
        public void ResetRunAxis()
        {
            _isStartZDirection = null;
        }

        // blockCauses・beltReasonsは返り値のPlaceInfo列と同じ添字で並走する不可原因の列
        // blockCauses and beltReasons are block-cause columns indexed like the returned PlaceInfo list
        public List<PlaceInfo> Build(Vector3Int dragStartPoint, Vector3Int placePoint, BlockDirection blockDirection, BeltConveyorHoldingBlock holdingBlock, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)
        {
            // 起点セルに戻れば軸は未決に戻り、離れた最初の1回だけ長い方の軸を先行にする
            // Returning to the start cell clears the axis; the longer side becomes the leading axis only on the first departure
            if (dragStartPoint == placePoint)
            {
                _isStartZDirection = null;
            }
            else if (!_isStartZDirection.HasValue)
            {
                _isStartZDirection = Mathf.Abs(placePoint.x - dragStartPoint.x) < Mathf.Abs(placePoint.z - dragStartPoint.z);
            }

            // 坂選択中は一定勾配の専用経路。立体交差も坂の自動割り当ても通さない
            // A selected slope uses the constant-grade path: neither the overpass nor the auto slope assignment runs
            if (holdingBlock.IsSlopeSelected)
            {
                return _placePointCalculator.CalculateSlopePoint(dragStartPoint, placePoint, _isStartZDirection ?? true, blockDirection, holdingBlock.BlockMaster, holdingBlock.SlopeDirection, out blockCauses, out beltReasons);
            }

            var cellInfos = _placePointCalculator.CalculatePoint(dragStartPoint, placePoint, _isStartZDirection ?? true, blockDirection, holdingBlock.BlockMaster, out blockCauses, out beltReasons);

            // セル列へ直線・坂ブロックを1対1で割り当てる（坂欠落はベルト固有理由の列へ書き戻される）
            // Assign straight and slope blocks to cells one-to-one (a missing slope is written back into the belt reason column)
            return BeltConveyorCellBlockResolver.Resolve(cellInfos, holdingBlock.Family, beltReasons);
        }
    }
}
