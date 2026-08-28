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
            var blockSize = holdingBlockMasterElement.BlockSize;

            List<Vector3Int> positions = CalcPositions(blockSize, out var runAxis);

            List<PlaceInfo> cells = CalcPlaceCells(positions);

            var blockCauses = new List<PlacementBlockCause>(cells.Count);
            for (var i = 0; i < cells.Count; i++) blockCauses.Add(PlacementBlockCause.None);

            return new PlacementRun(cells, blockCauses, runAxis, ResolveCursorIndex(positions));

            #region Internal

            List<Vector3Int> CalcPositions(Vector3Int size, out PlacementRunAxis extendedAxis)
            {
                var pointList = new List<Vector3Int>();
                var currentPoint = startPoint;
                pointList.Add(currentPoint);
                
                // 最も距離が長い方向を判定
                var deltaX = Mathf.Abs(endPoint.x - startPoint.x);
                var deltaY = Mathf.Abs(endPoint.y - startPoint.y);
                var deltaZ = Mathf.Abs(endPoint.z - startPoint.z);
                
                if (deltaX >= deltaY && deltaX >= deltaZ)
                {
                    // X方向に伸ばす
                    extendedAxis = PlacementRunAxis.X;
                    var stepX = size.x;
                    var directionX = endPoint.x > startPoint.x ? 1 : -1;
                    
                    while (Mathf.Abs(currentPoint.x - endPoint.x) >= stepX)
                    {
                        currentPoint.x += stepX * directionX;
                        pointList.Add(currentPoint);
                    }
                }
                else if (deltaZ >= deltaX && deltaZ >= deltaY)
                {
                    // Z方向に伸ばす
                    extendedAxis = PlacementRunAxis.Z;
                    var stepZ = size.z;
                    var directionZ = endPoint.z > startPoint.z ? 1 : -1;
                    
                    while (Mathf.Abs(currentPoint.z - endPoint.z) >= stepZ)
                    {
                        currentPoint.z += stepZ * directionZ;
                        pointList.Add(currentPoint);
                    }
                }
                else
                {
                    // Y方向に伸ばす
                    extendedAxis = PlacementRunAxis.Y;
                    var stepY = size.y;
                    var directionY = endPoint.y > startPoint.y ? 1 : -1;
                    
                    while (Mathf.Abs(currentPoint.y - endPoint.y) >= stepY)
                    {
                        currentPoint.y += stepY * directionY;
                        pointList.Add(currentPoint);
                    }
                }
                
                return pointList;
            }
            
            List<PlaceInfo> CalcPlaceCells(List<Vector3Int> placePositions)
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

            // 終点は刻み幅で割り切れないと列に載らないため、一致が無ければ末尾セルを充てる
            // The end point is not on the run when the step does not divide it, so the last cell stands in
            int ResolveCursorIndex(List<Vector3Int> placePositions)
            {
                for (var i = 0; i < placePositions.Count; i++)
                {
                    if (placePositions[i] == endPoint) return i;
                }

                return placePositions.Count - 1;
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
