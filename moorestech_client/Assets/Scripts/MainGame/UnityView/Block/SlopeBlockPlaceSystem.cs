using System;
using System.Collections.Generic;
using Game.World.Interface.DataStore;
using Constant;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class SlopeBlockPlaceSystem
    {
        
        public static readonly int GroundLayerMask = LayerMask.GetMask("Ground");
        
        public static (Vector3 position, Quaternion rotation, Vector3 scale) GetSlopeBeltConveyorTransform(Vector2Int blockPosition,BlockDirection blockDirection,Vector2Int blockSize)
        {
            //実際のブロックのモデルは+0.5した値が中心になる
            var blockObjectPos = blockPosition.AddBlockPlaceOffset();

            var frontPoint = GetGroundPoint(GetBlockFrontRayOffset(blockDirection) + blockObjectPos);
            var backPoint = GetGroundPoint(-GetBlockFrontRayOffset(blockDirection) + blockObjectPos);
            
            //斜辺の長さを求める
            var hypotenuse = Vector3.Distance(frontPoint, backPoint);
            //高さを求める
            var height = Mathf.Abs(frontPoint.y - backPoint.y);
            var blockY = GetBlockFourCornerMaxHeight(blockPosition, blockDirection, blockSize);
            //角度を求める
            var blockAngle = Mathf.Asin(height / hypotenuse) * Mathf.Rad2Deg;
            
            
            var resultBlockPos = new Vector3(blockObjectPos.x,blockY + 0.3f,blockObjectPos.y);
            var blockRotation = GetRotation(blockDirection, blockAngle,frontPoint.y > backPoint.y);
            var blockScale = new Vector3(1, 1, hypotenuse);
            
            return (resultBlockPos, blockRotation, blockScale);
        }

        public static Vector3 GetGroundPoint(Vector2 pos,Color debugRayColor = default)
        {
            var checkRay = new Ray(new Vector3(pos.x,1000,pos.y), Vector3.down);
            Debug.DrawRay(checkRay.origin, checkRay.direction * 1000, debugRayColor, 3);
            
            if (!Physics.Raycast(checkRay, out var checkHit, 1500, GroundLayerMask))
            {
                throw new Exception("地面が見つかりませんでした");
            }
            return checkHit.point;
        }

        public static float GetBlockFourCornerMaxHeight(Vector2Int blockPos,BlockDirection blockDirection,Vector2Int blockSize)
        {
            var (minPos,maxPos) = blockPos.GetWorldBlockBoundingBox(blockDirection, blockSize);
            var heights = new List<float>
            {
                GetGroundPoint(new Vector2(minPos.x, minPos.y),Color.red).y,
                GetGroundPoint(new Vector2(minPos.x, maxPos.y),Color.magenta).y,
                GetGroundPoint(new Vector2(maxPos.x, minPos.y),Color.cyan).y,
                GetGroundPoint(new Vector2(maxPos.x, maxPos.y),Color.blue).y
            };

            return Mathf.Max(heights.ToArray());
        }
        
        
        private static Vector2 GetBlockFrontRayOffset(BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.North => new Vector2(0, 0.5f),
                BlockDirection.East => new Vector2(0.5f, 0),
                BlockDirection.South => new Vector2(0, -0.5f),
                BlockDirection.West => new Vector2(-0.5f, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(blockDirection), blockDirection, null)
            };
        }

        private static Quaternion GetRotation(BlockDirection blockDirection, float blockAngle,bool isFrontUp)
        {
            blockAngle = isFrontUp ? -blockAngle : blockAngle;
            var defaultAngle = BlockDirectionAngle.GetRotation(blockDirection).eulerAngles;
            return Quaternion.Euler(blockAngle, defaultAngle.y, defaultAngle.z);
        }
    }
}