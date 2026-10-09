using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     ドラッグ列の生成と、確定した列への既存ブロック重なり評価を担う
    ///     Builds a drag run and evaluates existing-block overlaps once the run is final
    ///     生成と評価を分けているのは、両者の間で地形追従がYを書き換えるため
    ///     They are separate because terrain following rewrites Y between the two
    /// </summary>
    public class CommonBlockPlacePointCalculator : IExistingBlockQuery
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        
        public CommonBlockPlacePointCalculator(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }
        
        // 列の骨格だけを作る。この時点の不可原因はすべてNoneで、Yが確定してから評価される
        // Builds only the run skeleton; every block cause is None here and gets evaluated once Y is final
        public static PlacementRun CalculateRun(Vector3Int startPoint, Vector3Int endPoint, BlockDirection blockDirection, BlockMasterElement holdingBlockMasterElement)
        {
            var runPositions = PlacementRunPositionCalculator.Calculate(startPoint, endPoint, holdingBlockMasterElement.BlockSize);
            var cells = CalcPlaceCells(runPositions.Positions);
            var blockCauses = new List<PlacementBlockCause>(cells.Count);
            for (var i = 0; i < cells.Count; i++) blockCauses.Add(PlacementBlockCause.None);

            return new PlacementRun(cells, blockCauses, runPositions.Axis, runPositions.CursorIndex);

            #region Internal

            List<PlaceInfo> CalcPlaceCells(IReadOnlyList<Vector3Int> placePositions)
            {
                var placeInfos = new List<PlaceInfo>(placePositions.Count);

                foreach (var placePosition in placePositions)
                {
                    var placeInfo = new PlaceInfo
                    {
                        Position = placePosition,
                        Direction = blockDirection,
                        VerticalDirection = BlockVerticalDirection.Horizontal,
                        Placeable = true,
                    };

                    // ゼロGuidは実ブロックに解決されない未解決値として扱う（純粋ロジックテストのモック要素）
                    // A zero Guid is treated as an unresolved value that never resolves to a real block (used by pure-logic test mocks)
                    if (holdingBlockMasterElement.BlockGuid != Guid.Empty)
                    {
                        placeInfo.BlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMasterElement.BlockGuid);
                    }

                    placeInfos.Add(placeInfo);
                }

                return placeInfos;
            }

            #endregion
        }
        
        // Y確定後の列へ既存ブロックの重なりを反映する。既に別の原因が立っているセルは触らない
        // Applies existing-block overlaps to a run whose Y is final; cells that already carry another cause stay untouched
        public static void EvaluateExistingBlockCauses(PlacementRun run, IExistingBlockQuery existingBlockQuery)
        {
            for (var i = 0; i < run.Cells.Count; i++)
            {
                var placeInfo = run.Cells[i];
                if (run.BlockCauses[i] != PlacementBlockCause.None || !placeInfo.Placeable) continue;
                if (!existingBlockQuery.IsOverlapping(placeInfo)) continue;

                placeInfo.Placeable = false;
                run.BlockCauses[i] = PlacementBlockCause.ExistingBlock;
            }
        }

        public void EvaluateExistingBlockCauses(PlacementRun run)
        {
            EvaluateExistingBlockCauses(run, this);
        }

        // 設置予定地にブロックが既に存在しているかどうか
        // Whether a block already occupies the planned placement cell
        public bool IsOverlapping(PlaceInfo placeInfo)
        {
            var size = MasterHolder.BlockMaster.GetBlockMaster(placeInfo.BlockId).BlockSize;
            var previewPositionInfo = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, size);

            return _blockGameObjectDataStore.IsOverlapPositionInfo(previewPositionInfo);
        }
    }
}
