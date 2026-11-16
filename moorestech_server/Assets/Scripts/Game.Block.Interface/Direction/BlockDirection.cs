using System;
using UnityEngine;

namespace Game.Block.Interface
{
    /// <summary>
    ///     ブロックの水平4方向の向き。上下に回した設置は無い
    ///     The block's four horizontal headings; there is no up/down placement
    /// </summary>
    // North=0..West=3 は時計回りで、セーブ形式の契約（整数で保存）
    // North=0..West=3 run clockwise and are a save-format contract (stored as integers)
    public enum BlockDirection
    {
        North,
        East,
        South,
        West,
    }
    
    public delegate Vector3Int BlockPosConvertAction(Vector3Int pos);
    
    public static class BlockDirectionExtension
    {
        public static Quaternion GetRotation(this BlockDirection direction)
        {
            switch (direction)
            {
                case BlockDirection.North:
                    return Quaternion.Euler(0, 0, 0);
                case BlockDirection.East:
                    return Quaternion.Euler(0, 90, 0);
                case BlockDirection.South:
                    return Quaternion.Euler(0, 180, 0);
                case BlockDirection.West:
                    return Quaternion.Euler(0, 270, 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
        
        /// <summary>
        ///     ブロックローカルのセルを向きに応じて回転させる。原点の加算はしないので設置位置は呼び出し側で足す
        ///     Rotates a block-local cell by the direction; the origin is not added, so callers add the placement position
        /// </summary>
        public static Vector3Int ConvertLocalCell(this BlockDirection blockDirection, Vector3Int localCell)
        {
            // 行列は float4 × float4 の形なので拡張して計算し、結果を Vector3Int に丸める
            // The matrix works on float4 x float4, so transform then round the result back to Vector3Int
            var rotationMatrix = Matrix4x4.Rotate(blockDirection.GetRotation());
            return Vector3Int.RoundToInt(rotationMatrix.MultiplyPoint3x4(localCell));
        }

        public static BlockPosConvertAction GetCoordinateConvertAction(this BlockDirection blockDirection)
        {
            return pos => blockDirection.ConvertLocalCell(pos);
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
            var addPos = blockDirection switch
            {
                BlockDirection.North => new Vector3Int(0, 0, 0),
                BlockDirection.East => new Vector3Int(0, 0, size.x),
                BlockDirection.South => new Vector3Int(size.x, 0, size.z),
                BlockDirection.West => new Vector3Int(size.z, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(blockDirection), blockDirection, null),
            };
            return pos + addPos;
        }
        
        public static Vector3Int GetBlockDirectionOffset(this BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.North => new Vector3Int(0, 0, 0),
                BlockDirection.East => new Vector3Int(0, 0, 1),
                BlockDirection.South => new Vector3Int(1, 0, 1),
                BlockDirection.West => new Vector3Int(1, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(blockDirection), blockDirection, null),
            };
        }
    }
}
