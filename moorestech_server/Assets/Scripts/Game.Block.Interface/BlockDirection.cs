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
            switch (blockDirection)
            {
                case BlockDirection.UpNorth:
                    return p => new Vector3Int(p.x, p.z, -p.y);
                case BlockDirection.UpEast:
                    return p => new Vector3Int(-p.y, p.z, -p.x);
                case BlockDirection.UpSouth:
                    return p => new Vector3Int(-p.x, p.z, p.y);
                case BlockDirection.UpWest:
                    return p => new Vector3Int(p.y, p.z, p.x);
                
                case BlockDirection.North:
                    return p => p;
                case BlockDirection.East:
                    return p => new Vector3Int(p.z, p.y, -p.x);
                case BlockDirection.South:
                    return p => new Vector3Int(-p.x, p.y, -p.z);
                case BlockDirection.West:
                    return p => new Vector3Int(-p.z, p.y, p.x);
                
                case BlockDirection.DownNorth:
                    return p => new Vector3Int(-p.x, -p.z, -p.y);
                case BlockDirection.DownEast:
                    return p => new Vector3Int(-p.y, -p.z, p.x);
                case BlockDirection.DownSouth:
                    return p => new Vector3Int(p.x, -p.z, p.y);
                case BlockDirection.DownWest:
                    return p => new Vector3Int(p.y, -p.z, -p.x);
                default:
                    throw new ArgumentOutOfRangeException(nameof(blockDirection), blockDirection, null);
            }
        }
        
        public static Vector3Int GetBlockModelOriginPos(this BlockDirection blockDirection, BlockPositionInfo blockPositionInfo)
        {
            var pos = blockPositionInfo.OriginalPos;
            var size = blockPositionInfo.BlockSize;
            return blockDirection.GetBlockModelOriginPos(pos, size);
        }
        
        public static Vector3Int GetBlockModelOriginPos(this BlockDirection blockDirection, Vector3Int pos, Vector3Int size)
        {
            var addPos = Vector3Int.zero;
            switch (blockDirection)
            {
                case BlockDirection.UpNorth:
                    addPos = new Vector3Int(0, 0, size.y);
                    break;
                case BlockDirection.UpEast:
                    addPos = new Vector3Int(size.y, 0, size.x);
                    break;
                case BlockDirection.UpSouth:
                    addPos = new Vector3Int(size.x, 0, size.y);
                    break;
                case BlockDirection.UpWest:
                    addPos = new Vector3Int(0, 0, size.y);
                    break;
                
                case BlockDirection.North:
                    addPos = new Vector3Int(0, 0, 0);
                    break;
                case BlockDirection.East:
                    addPos = new Vector3Int(0, 0, size.x);
                    break;
                case BlockDirection.South:
                    addPos = new Vector3Int(size.x, 0, size.z);
                    break;
                case BlockDirection.West:
                    addPos = new Vector3Int(size.z, 0, 0);
                    break;
                
                case BlockDirection.DownNorth:
                    addPos = new Vector3Int(size.x, size.z, size.y);
                    break;
                case BlockDirection.DownEast:
                    addPos = new Vector3Int(size.y, size.z, 0);
                    break;
                case BlockDirection.DownSouth:
                    addPos = new Vector3Int(0, size.z, 0);
                    break;
                case BlockDirection.DownWest:
                    addPos = new Vector3Int(0, size.z, size.x);
                    break;
            }
            
            return pos + addPos;
        }
        
        public static Vector3Int RotationPosition(this BlockDirection blockDirection, Vector3Int originPos, Vector3Int targetPos)
        {
            var originBaseTargetPos = targetPos - originPos;
            var convertAction = blockDirection.GetCoordinateConvertAction();
            
            return convertAction(originBaseTargetPos) + originPos;
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