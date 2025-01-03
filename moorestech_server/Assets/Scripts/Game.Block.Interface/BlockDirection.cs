using System;
using UnityEngine;

namespace Game.Block.Interface
{
    /// <summary>
    ///     3次元的な方向に配置するため、上、下、通常の設置×4方向の向きが必要になる
    ///     UpNorthは、ブロックを上方向にした後、通常の設置をした時の下の面が北向きになることを意味する
    ///     DownNorthは、ブロックを下方向にした後、通常の設置をした時の下の面が北向きになることを意味する
    /// </summary>
    public enum BlockDirection
    {
        UpNorth,
        UpEast,
        UpSouth,
        UpWest,
        
        North,
        East,
        South,
        West,
        
        DownNorth,
        DownEast,
        DownSouth,
        DownWest,
    }
    
    public delegate Vector3Int BlockPosConvertAction(Vector3Int pos);
    
    public static class BlockDirectionExtension
    {
        public static Quaternion GetRotation(this BlockDirection direction)
        {
            switch (direction)
            {
                case BlockDirection.UpNorth:
                    return Quaternion.Euler(-90, 0, 0);
                case BlockDirection.UpEast:
                    return Quaternion.Euler(-90, 0, 90);
                case BlockDirection.UpSouth:
                    return Quaternion.Euler(-90, 0, 180);
                case BlockDirection.UpWest:
                    return Quaternion.Euler(-90, 0, 270);
                
                case BlockDirection.North:
                    return Quaternion.Euler(0, 0, 0);
                case BlockDirection.East:
                    return Quaternion.Euler(0, 90, 0);
                case BlockDirection.South:
                    return Quaternion.Euler(0, 180, 0);
                case BlockDirection.West:
                    return Quaternion.Euler(0, 270, 0);
                
                case BlockDirection.DownNorth:
                    return Quaternion.Euler(90, 0, 180);
                case BlockDirection.DownEast:
                    return Quaternion.Euler(90, 0, 90);
                case BlockDirection.DownSouth:
                    return Quaternion.Euler(90, 0, 0);
                case BlockDirection.DownWest:
                    return Quaternion.Euler(90, 0, 270);
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
        
        public static BlockPosConvertAction GetCoordinateConvertAction(this BlockDirection blockDirection)
        {
            var rotation = blockDirection.GetRotation();
            var rotationMatrix = Matrix4x4.Rotate(rotation);
            
            // 変換処理を返す
            return pos =>
            {
                // 行列は float4 × float4 の形なので pos を拡張して計算
                var transformed = rotationMatrix.MultiplyPoint3x4(pos);
                // 戻り値は Vector3Int に丸め
                return Vector3Int.RoundToInt(transformed);
            };
        }
        
        /// <summary>
        /// そのブロックが回転している時、そのブロック座標系の基準座標が、ワールドのどこにあるかを返す
        /// When the block is rotating, return the world position of the reference coordinate in the block's local coordinate system.
        /// </summary>
        public static Vector3Int GetBlockBaseOriginPos(this BlockDirection blockDirection, BlockPositionInfo blockPositionInfo)
        {
            var pos = blockPositionInfo.OriginalPos;
            var size = blockPositionInfo.BlockSize;
            
            var minus = blockDirection.GetBlockDirectionOffset() * Vector3Int.one;
            var originPos = blockDirection.GetBlockModelOriginPos(pos, size);
            
            return originPos - minus;
        }
        
        
        public static Vector3Int GetBlockModelOriginPos(this BlockDirection blockDirection, Vector3Int pos, Vector3Int size)
        {
            var addPos = blockDirection.GetBlockDirectionOffset() * size;
            
            return pos + addPos;
        }
        
        public static Vector3Int GetBlockDirectionOffset(this BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.UpNorth => new Vector3Int(0, 0, 1),
                BlockDirection.UpEast => new Vector3Int(1, 0, 1),
                BlockDirection.UpSouth => new Vector3Int(1, 0, 1),
                BlockDirection.UpWest => new Vector3Int(0, 0, 1),
                BlockDirection.North => new Vector3Int(0, 0, 0),
                BlockDirection.East => new Vector3Int(0, 0, 1),
                BlockDirection.South => new Vector3Int(1, 0, 1),
                BlockDirection.West => new Vector3Int(1, 0, 0),
                BlockDirection.DownNorth => new Vector3Int(1, 1, 1),
                BlockDirection.DownEast => new Vector3Int(1, 1, 0),
                BlockDirection.DownSouth => new Vector3Int(0, 1, 0),
                BlockDirection.DownWest => new Vector3Int(0, 1, 1),
                _ => Vector3Int.zero
            };
        }
        
        public static BlockDirection HorizonRotation(this BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.UpNorth => BlockDirection.UpEast,
                BlockDirection.UpEast => BlockDirection.UpSouth,
                BlockDirection.UpSouth => BlockDirection.UpWest,
                BlockDirection.UpWest => BlockDirection.UpNorth,
                
                BlockDirection.North => BlockDirection.East,
                BlockDirection.East => BlockDirection.South,
                BlockDirection.South => BlockDirection.West,
                BlockDirection.West => BlockDirection.North,
                
                BlockDirection.DownNorth => BlockDirection.DownEast,
                BlockDirection.DownEast => BlockDirection.DownSouth,
                BlockDirection.DownSouth => BlockDirection.DownWest,
                BlockDirection.DownWest => BlockDirection.DownNorth,
                
                _ => blockDirection
            };
        }
        
        public static BlockDirection VerticalRotation(this BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.UpNorth => BlockDirection.DownNorth,
                BlockDirection.UpEast => BlockDirection.DownEast,
                BlockDirection.UpSouth => BlockDirection.DownSouth,
                BlockDirection.UpWest => BlockDirection.DownWest,
                
                BlockDirection.North => BlockDirection.UpNorth,
                BlockDirection.East => BlockDirection.UpEast,
                BlockDirection.South => BlockDirection.UpSouth,
                BlockDirection.West => BlockDirection.UpWest,
                
                BlockDirection.DownNorth => BlockDirection.North,
                BlockDirection.DownEast => BlockDirection.East,
                BlockDirection.DownSouth => BlockDirection.South,
                BlockDirection.DownWest => BlockDirection.West,
                
                _ => blockDirection
            };
        }
    }
}