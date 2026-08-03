using System;
using System.Collections.Generic;
using Client.Common;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem
{
    public class SlopeBlockPlaceSystem
    {
        public static readonly int GroundLayerMask = LayerMask.GetMask("Ground");

        // 地表探査のレイ始点高さと探査距離。地形の最高点より十分上から、最低点より下まで貫く
        // Ray start height and probe length of ground probing: from well above the highest terrain to below the lowest
        private const float GroundProbeStartHeight = 1000f;
        private const float GroundProbeDistance = 1500f;

        /// <summary>
        ///     TODO ここの定義の場所を変える
        /// </summary>
        public static Vector3 GetBlockPositionToPlacePosition(Vector3Int blockPosition, BlockDirection blockDirection, BlockId blockId)
        {
            // 大きさをBlockDirection系に変換
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
            var originPos = blockDirection.GetBlockModelOriginPos(blockPosition, blockSize);
            
            return originPos;
        }
        
        [Obsolete("一応残してある")]
        public static (Vector3 position, Quaternion rotation, Vector3 scale) GetSlopeBeltConveyorTransform(string blockType, Vector3Int blockPosition, BlockDirection blockDirection, Vector3Int blockSize)
        {
            //実際のブロックのモデルは+0.5した値が中心になる
            var blockObjectPos = blockPosition.AddBlockPlaceOffset(); //TODo ←システムが変わったのでおそらくこの行は不要
            
            var frontPoint = GetGroundPoint(GetBlockFrontRayOffset(blockDirection) + blockObjectPos).Value; //TODO null check
            var backPoint = GetGroundPoint(-GetBlockFrontRayOffset(blockDirection) + blockObjectPos).Value;
            
            //斜辺の長さを求める
            var hypotenuse = Vector3.Distance(frontPoint, backPoint);
            //高さを求める
            var height = Mathf.Abs(frontPoint.y - backPoint.y);
            var blockY = GetBlockFourCornerMaxHeight(blockPosition, blockDirection, blockSize);
            //角度を求める
            var blockAngle = Mathf.Asin(height / hypotenuse) * Mathf.Rad2Deg;
            
            
            var resultBlockPos = new Vector3(blockObjectPos.x, blockY + 0.3f, blockObjectPos.y);
            var blockRotation = GetRotation(blockDirection, blockAngle, frontPoint.y > backPoint.y);
            var blockScale = new Vector3(1, 1, hypotenuse);
            
            if (!BlockSlopeDeformationType.IsDeformation(blockType))
            {
                blockRotation = blockDirection.GetRotation();
                blockScale = Vector3.one;
            }
            
            return (resultBlockPos, blockRotation, blockScale);
        }
        
        // 地表探査の単一エントリポイント。XZだけを取りY成分の取り違えを署名で封じる。露頭など大量プローブ用にログ無しで成否を返す
        // Single entry point of ground probing; taking only XZ makes a mistaken Y impossible, and bulk probes such as outcrops get the outcome without logging
        public static bool TryGetGroundPoint(float worldX, float worldZ, out Vector3 groundPoint)
        {
            var checkRay = new Ray(new Vector3(worldX, GroundProbeStartHeight, worldZ), Vector3.down);
            if (Physics.Raycast(checkRay, out var checkHit, GroundProbeDistance, GroundLayerMask))
            {
                groundPoint = checkHit.point;
                return true;
            }
            groundPoint = default;
            return false;
        }

        public static Vector3? GetGroundPoint(Vector3 pos)
        {
            return GetGroundPoint(pos, default);
        }

        public static Vector3? GetGroundPoint(Vector3 pos, Color debugRayColor)
        {
            Debug.DrawRay(new Vector3(pos.x, GroundProbeStartHeight, pos.z), Vector3.down * GroundProbeDistance, debugRayColor, 3);

            if (!TryGetGroundPoint(pos.x, pos.z, out var groundPoint))
            {
                Debug.LogError("地面が見つかりませんでした pos:" + pos + " layer:" + GroundLayerMask);
                return null;
            }
            return groundPoint;
        }

        public static float GetBlockFourCornerMaxHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize)
        {
            var (minPos, maxPos) = blockPos.GetWorldBlockBoundingBox(blockDirection, blockSize);

            // boundingBoxは3次元なので水平の四隅はXとZで組む。Vector2の暗黙変換に任せると鉛直Yを渡してz=0を探査してしまう
            // The bounding box is 3D, so the horizontal corners pair X with Z; the Vector2 conversion would pass the vertical Y and probe z=0
            var heights = new List<float>
            {
                ProbeCornerHeight(minPos.x, minPos.z),
                ProbeCornerHeight(minPos.x, maxPos.z),
                ProbeCornerHeight(maxPos.x, minPos.z),
                ProbeCornerHeight(maxPos.x, maxPos.z),
            };

            return Mathf.Max(heights.ToArray());

            #region Internal

            float ProbeCornerHeight(float worldX, float worldZ)
            {
                if (TryGetGroundPoint(worldX, worldZ, out var groundPoint)) return groundPoint.y;
                throw new InvalidOperationException($"四隅の地表が見つかりませんでした x:{worldX} z:{worldZ}");
            }

            #endregion
        }
        
        private static Vector3 GetBlockFrontRayOffset(BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.North => new Vector3(0, 0, 0.5f),
                BlockDirection.East => new Vector3(0.5f, 0, 0),
                BlockDirection.South => new Vector3(0, 0, -0.5f),
                BlockDirection.West => new Vector3(-0.5f, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(blockDirection), blockDirection, null),
            };
        }
        
        private static Quaternion GetRotation(BlockDirection blockDirection, float blockAngle, bool isFrontUp)
        {
            blockAngle = isFrontUp ? -blockAngle : blockAngle;
            var defaultAngle = blockDirection.GetRotation().eulerAngles;
            return Quaternion.Euler(blockAngle, defaultAngle.y, defaultAngle.z);
        }
    }
}