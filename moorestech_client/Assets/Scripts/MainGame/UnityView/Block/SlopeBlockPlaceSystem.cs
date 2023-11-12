using System;
using Game.World.Interface.DataStore;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class SlopeBlockPlaceSystem
    {
        
        public static readonly int GroundLayerMask = LayerMask.GetMask("Ground");
        
        public static (Vector3 position, Quaternion rotation, Vector3 scale) GetSlopeBeltConveyorTransform(Vector3 position,BlockDirection blockDirection)
        {
            var checkFrontPoint = GetRayOffset(blockDirection) + position;
            var checkBackPoint = -GetRayOffset(blockDirection) + position;
            
            
            var checkFrontRay = new Ray(new Vector3(checkFrontPoint.x,1000,checkFrontPoint.z), Vector3.down);
            var checkBackRay = new Ray(new Vector3(checkBackPoint.x,1000,checkBackPoint.z), Vector3.down);
            
            if (!Physics.Raycast(checkFrontRay, out var checkFrontHit, 1500, GroundLayerMask) ||
                !Physics.Raycast(checkBackRay, out var checkBackHit, 1500, GroundLayerMask))
            {
                throw new Exception("地面が見つかりませんでした");
            }
            
            var frontPoint = checkFrontHit.point;
            var backPoint = checkBackHit.point;
            
            //斜辺の長さを求める
            var hypotenuse = Vector3.Distance(frontPoint, backPoint);
            //高さを求める
            var height = Mathf.Abs(frontPoint.y - backPoint.y);
            var blockY = (frontPoint.y + backPoint.y) / 2;
            //角度を求める
            var blockAngle = Mathf.Asin(height / hypotenuse) * Mathf.Rad2Deg;
            
            
            var blockPosition = new Vector3(position.x,blockY + 0.3f,position.z);
            var blockRotation = GetRotation(blockDirection, blockAngle,frontPoint.y > backPoint.y);
            var blockScale = new Vector3(1, 1, hypotenuse);
            
            return (blockPosition, blockRotation, blockScale);
        }

        private static Vector3 GetRayOffset(BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.North => new Vector3(0, 0, 0.5f),
                BlockDirection.East => new Vector3(0.5f, 0, 0),
                BlockDirection.South => new Vector3(0, 0, -0.5f),
                BlockDirection.West => new Vector3(-0.5f, 0, 0),
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