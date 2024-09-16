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
        
        /// <summary>
        ///     TODO ここの定義の場所を変える
        /// </summary>
        public static Vector3 GetBlockPositionToPlacePosition(Vector3Int blockPosition, BlockDirection blockDirection, BlockId blockId)
        {
            // 大きさをBlockDirection系に変換
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
            var originPos = blockDirection.GetBlockOriginPos(blockPosition, blockSize);
            
            return originPos;
        }
        
        [Obsolete("一応残してある")]
        public static (Vector3 position, Quaternion rotation, Vector3 scale) GetSlopeBeltConveyorTransform(string blockType, Vector3Int blockPosition, BlockDirection blockDirection, Vector3Int blockSize)
        {
            //実際のブロックのモデルは+0.5した値が中心になる
            var blockObjectPos = blockPosition.AddBlockPlaceOffset(); //TODo ←システムが変わったのでおそらくこの行は不要
            
            var frontPoint = GetGroundPoint(GetBlockFrontRayOffset(blockDirection) + blockObjectPos);
            var backPoint = GetGroundPoint(-GetBlockFrontRayOffset(blockDirection) + blockObjectPos);
            
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
        
        public static Vector3 GetGroundPoint(Vector3 pos, Color debugRayColor = default)
        {
            var checkRay = new Ray(new Vector3(pos.x, 1000, pos.z), Vector3.down);
            Debug.DrawRay(checkRay.origin, checkRay.direction * 1000, debugRayColor, 3);
            
            if (!Physics.Raycast(checkRay, out var checkHit, 1500, GroundLayerMask)) throw new Exception("地面が見つかりませんでした pos:" + pos + " layer:" + GroundLayerMask);
            return checkHit.point;
        }
        
        public static float GetBlockFourCornerMaxHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize)
        {
            var (minPos, maxPos) = blockPos.GetWorldBlockBoundingBox(blockDirection, blockSize);
            var heights = new List<float>
            {
                GetGroundPoint(new Vector2(minPos.x, minPos.y), Color.red).y,
                GetGroundPoint(new Vector2(minPos.x, maxPos.y), Color.magenta).y,
                GetGroundPoint(new Vector2(maxPos.x, minPos.y), Color.cyan).y,
                GetGroundPoint(new Vector2(maxPos.x, maxPos.y), Color.blue).y,
            };
            
            return Mathf.Max(heights.ToArray());
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