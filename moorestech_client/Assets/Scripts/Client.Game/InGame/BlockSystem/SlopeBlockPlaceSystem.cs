using System;
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

        // 探査レイの描画色。呼び出し側から渡すと消費者不在の引数が残るため所有者側に固定する
        // Probe ray color, fixed on the owner because passing it in leaves a parameter with no consumer
        private static readonly Color GroundProbeRayColor = Color.red;

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
            
            var frontRayPos = GetBlockFrontRayOffset(blockDirection) + blockObjectPos;
            var backRayPos = -GetBlockFrontRayOffset(blockDirection) + blockObjectPos;
            var frontPoint = GetGroundPoint(frontRayPos.x, frontRayPos.z).Value; //TODO null check
            var backPoint = GetGroundPoint(backRayPos.x, backRayPos.z).Value;
            
            //斜辺の長さを求める
            var hypotenuse = Vector3.Distance(frontPoint, backPoint);
            //高さを求める
            var height = Mathf.Abs(frontPoint.y - backPoint.y);
            var blockY = GetBlockFourCornerMaxHeight(blockPosition, blockDirection, blockSize);
            //角度を求める
            var blockAngle = Mathf.Asin(height / hypotenuse) * Mathf.Rad2Deg;
            
            
            var resultBlockPos = new Vector3(blockObjectPos.x, blockY + 0.3f, blockObjectPos.z);
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

        // 探査失敗をログで知らせる入口。XZ明示なのはVector3を取るとVector2の暗黙変換でz=0を探査できてしまうため
        // Entry point that logs a failed probe; it takes XZ because a Vector3 parameter would let the Vector2 conversion probe z=0
        internal static Vector3? GetGroundPoint(float worldX, float worldZ)
        {
            Debug.DrawRay(new Vector3(worldX, GroundProbeStartHeight, worldZ), Vector3.down * GroundProbeDistance, GroundProbeRayColor, 3);

            if (!TryGetGroundPoint(worldX, worldZ, out var groundPoint))
            {
                Debug.LogError($"地面が見つかりませんでした x:{worldX} z:{worldZ} layer:{GroundLayerMask}");
                return null;
            }
            return groundPoint;
        }

        // 四隅すべて取れたときだけ最高点を返す
        // Returns the max height only when all four corners hit ground
        public static bool TryGetBlockFourCornerMaxHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize, out float maxHeight)
        {
            maxHeight = 0f;
            var (minPos, maxPos) = blockPos.GetWorldBlockBoundingBox(blockDirection, blockSize);

            // boundingBoxは3次元なので水平の四隅はXとZで組む。Vector2の暗黙変換に任せると鉛直Yを渡してz=0を探査してしまう
            // The bounding box is 3D, so the horizontal corners pair X with Z; the Vector2 conversion would pass the vertical Y and probe z=0
            if (!TryProbeCornerHeight(minPos.x, minPos.z, out var minXMinZ)) return false;
            if (!TryProbeCornerHeight(minPos.x, maxPos.z, out var minXMaxZ)) return false;
            if (!TryProbeCornerHeight(maxPos.x, minPos.z, out var maxXMinZ)) return false;
            if (!TryProbeCornerHeight(maxPos.x, maxPos.z, out var maxXMaxZ)) return false;

            maxHeight = Mathf.Max(Mathf.Max(minXMinZ, minXMaxZ), Mathf.Max(maxXMinZ, maxXMaxZ));
            return true;

            #region Internal

            bool TryProbeCornerHeight(float worldX, float worldZ, out float height)
            {
                if (!TryGetGroundPoint(worldX, worldZ, out var groundPoint))
                {
                    height = 0f;
                    return false;
                }
                height = groundPoint.y;
                return true;
            }

            #endregion
        }

        public static float GetBlockFourCornerMaxHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize)
        {
            if (!TryGetBlockFourCornerMaxHeight(blockPos, blockDirection, blockSize, out var maxHeight))
                throw new InvalidOperationException($"四隅の地表が見つかりませんでした blockPos:{blockPos}");
            return maxHeight;
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