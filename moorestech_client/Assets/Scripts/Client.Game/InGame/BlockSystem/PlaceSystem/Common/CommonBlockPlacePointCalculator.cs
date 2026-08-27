using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    public class CommonBlockPlacePointCalculator
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        
        public CommonBlockPlacePointCalculator(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }
        
        // blockCausesはPlaceInfo列と同じ添字で並走するセル毎の設置不可原因（表示側へ明示的に渡すための列）
        // blockCauses is the per-cell block cause column indexed like the PlaceInfo list, handed explicitly to the display side
        public List<PlaceInfo> CalculatePoint(Vector3Int startPoint, Vector3Int endPoint, BlockDirection blockDirection, BlockMasterElement holdingBlockMasterElement, out List<PlacementBlockCause> blockCauses)
        {
            return CalculatePoint(startPoint, endPoint, blockDirection, holdingBlockMasterElement, IsNotExistBlock, out blockCauses);
        }

        public static List<PlaceInfo> CalculatePoint(Vector3Int startPoint, Vector3Int endPoint, BlockDirection blockDirection, BlockMasterElement holdingBlockMasterElement, Func<PlaceInfo, BlockMasterElement, bool> isNotExistBlock, out List<PlacementBlockCause> blockCauses)
        {
            // ひとまず、XとZ方向に目的地に向かって1ずつ進む
            var blockSize = holdingBlockMasterElement.BlockSize;

            List<Vector3Int> positions = CalcPositions(blockSize);

            List<PlaceInfo> placeInfos = CalcPlaceDirection(positions);

            blockCauses = CalcPlaceable(placeInfos);

            return placeInfos;

            #region Internal

            List<Vector3Int> CalcPositions(Vector3Int blockSize)
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
                    var stepX = blockSize.x;
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
                    var stepZ = blockSize.z;
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
                    var stepY = blockSize.y;
                    var directionY = endPoint.y > startPoint.y ? 1 : -1;
                    
                    while (Mathf.Abs(currentPoint.y - endPoint.y) >= stepY)
                    {
                        currentPoint.y += stepY * directionY;
                        pointList.Add(currentPoint);
                    }
                }
                
                return pointList;
            }
            
            List<PlaceInfo> CalcPlaceDirection(List<Vector3Int> placePositions)
            {
                var placeInfos = new List<PlaceInfo>(placePositions.Count);

                foreach (var placePosition in placePositions)
                {
                    placeInfos.Add(new PlaceInfo
                    {
                        Position = placePosition,
                        Direction = blockDirection,
                        VerticalDirection = BlockVerticalDirection.Horizontal,
                        Placeable = true,
                    });
                }

                return placeInfos;
            }

            List<PlacementBlockCause> CalcPlaceable(List<PlaceInfo> infos)
            {
                var causes = new List<PlacementBlockCause>(infos.Count);
                for (var i = 0; i < infos.Count; i++)
                {
                    var info = infos[i];
                    causes.Add(PlacementBlockCause.None);

                    // ゼロGuidは実ブロックに解決されない未解決値として扱う（純粋ロジックテストのモック要素）
                    // A zero Guid is treated as an unresolved value that never resolves to a real block (used by pure-logic test mocks)
                    if (holdingBlockMasterElement.BlockGuid != Guid.Empty)
                    {
                        info.BlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMasterElement.BlockGuid);
                    }

                    //TODO ブロックの数が足りているかどうか
                    if (info.Placeable && !isNotExistBlock(info, holdingBlockMasterElement))
                    {
                        info.Placeable = false;
                        causes[i] = PlacementBlockCause.ExistingBlock;
                    }
                }

                return causes;
            }

            #endregion
        }
        
        // Y確定後の位置で既存ブロックとの重なりを判定し直す。地形追従でYが変わると判定済みの結果が古くなるため
        // Re-checks overlaps with existing blocks once Y is final, because terrain following invalidates the earlier result
        public void RecalculateExistingBlockCauses(List<PlaceInfo> placeInfos, BlockMasterElement holdingBlockMasterElement, List<PlacementBlockCause> blockCauses)
        {
            // この段階の不可原因はExistingBlockのみなので、一度戻してから判定し直せる
            // ExistingBlock is the only cause at this stage, so the flags can be reset before re-checking
            for (var i = 0; i < placeInfos.Count; i++)
            {
                var placeInfo = placeInfos[i];
                placeInfo.Placeable = true;
                blockCauses[i] = PlacementBlockCause.None;

                if (IsNotExistBlock(placeInfo, holdingBlockMasterElement)) continue;

                placeInfo.Placeable = false;
                blockCauses[i] = PlacementBlockCause.ExistingBlock;
            }
        }

        // 設置予定地にブロックが既に存在しているかどうか
        private bool IsNotExistBlock(PlaceInfo placeInfo, BlockMasterElement holdingBlockMasterElement)
        {
            var size = MasterHolder.BlockMaster.GetBlockMaster(placeInfo.BlockId).BlockSize;
            var previewPositionInfo = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, size);

            return !_blockGameObjectDataStore.IsOverlapPositionInfo(previewPositionInfo);
        }
    }
}