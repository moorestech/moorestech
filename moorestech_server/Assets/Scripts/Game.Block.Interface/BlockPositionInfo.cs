using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Block.Interface
{
    public class BlockPositionInfo
    {
        public BlockPositionInfo(Vector3Int originalPos, BlockDirection blockDirection, Vector3Int blockSize)
        {
            OriginalPos = originalPos;
            BlockDirection = blockDirection;
            BlockSize = blockSize;
            
            MaxPos = CalcBlockMaxPos(originalPos, blockDirection, BlockSize);
        }
        /// <summary>
        ///     オリジナル座標は常に左下（ブロックが専有する範囲の最小の座標）になる
        /// </summary>
        public Vector3Int OriginalPos { get; }
        public Vector3Int BlockSize { get; }
        
        public Vector3Int MinPos => OriginalPos;
        public Vector3Int MaxPos { get; }
        
        public BlockDirection BlockDirection { get; }
        
        public bool IsContainPos(Vector3Int pos)
        {
            return OriginalPos.x <= pos.x && pos.x <= MaxPos.x &&
                   OriginalPos.y <= pos.y && pos.y <= MaxPos.y &&
                   OriginalPos.z <= pos.z && pos.z <= MaxPos.z;
        }
        
        /// <summary>
        ///     サーバー側管理のブロックの最大座標を計算する
        ///     これはどのグリッドにブロックが存在しているかということに使われるため、サイズ 1,1 の場合、originとmaxの値はおなじになる
        /// </summary>
        public static Vector3Int CalcBlockMaxPos(Vector3Int originPos, BlockDirection direction, Vector3Int blockSize)
        {
            var addPos = Vector3Int.zero;
            switch (direction)
            {
                case BlockDirection.UpNorth:
                case BlockDirection.UpSouth:
                case BlockDirection.DownNorth:
                case BlockDirection.DownSouth:
                    addPos = new Vector3Int(blockSize.x, blockSize.z, blockSize.y);
                    break;
                case BlockDirection.UpEast:
                case BlockDirection.UpWest:
                case BlockDirection.DownEast:
                case BlockDirection.DownWest:
                    addPos = new Vector3Int(blockSize.y, blockSize.z, blockSize.x);
                    break;
                
                case BlockDirection.North:
                case BlockDirection.South:
                    addPos = new Vector3Int(blockSize.x, blockSize.y, blockSize.z);
                    break;
                case BlockDirection.East:
                case BlockDirection.West:
                    addPos = new Vector3Int(blockSize.z, blockSize.y, blockSize.x);
                    break;
            }
            
            // block sizeは1からとなっているが、ここで求めるのはブロックが占める範囲の最大値なので、-1している
            return addPos + originPos - Vector3Int.one;
        }
        
        /// <summary>
        ///     <see cref="CalcBlockMaxPos" />をもとにバウンディングボックスを生成する
        /// </summary>
        public static BlockBoundingBox GetBlockBoundingBox(Vector3Int originPosition, BlockDirection direction, Vector3Int blockSize)
        {
            var maxPosition = CalcBlockMaxPos(originPosition, direction, blockSize) + Vector3Int.one;
            return new BlockBoundingBox(originPosition, maxPosition);
        }
        
        public readonly struct BlockBoundingBox : IEnumerable<Vector3Int>
        {
            public readonly Vector3Int MinPosition;
            public readonly Vector3Int MaxPosition;
            
            public BlockBoundingBox(Vector3Int minPosition, Vector3Int maxPosition)
            {
                MinPosition = Vector3Int.Min(minPosition, maxPosition);
                MaxPosition = Vector3Int.Max(minPosition, maxPosition);
            }
            
            public Enumerator GetEnumerator()
            {
                return new Enumerator(MinPosition, MaxPosition);
            }
            
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
            
            IEnumerator<Vector3Int> IEnumerable<Vector3Int>.GetEnumerator()
            {
                return GetEnumerator();
            }
            
            public struct Enumerator : IEnumerator<Vector3Int>
            {
                private Vector3Int _minPosition;
                private Vector3Int _maxPosition;
                public Enumerator(Vector3Int minPosition, Vector3Int maxPosition)
                {
                    _minPosition = minPosition;
                    Current = minPosition - new Vector3Int(0, 0, 1);
                    _maxPosition = maxPosition;
                }
                
                public void Dispose()
                {
                }
                
                public bool MoveNext()
                {
                    var x = Current.x;
                    var y = Current.y;
                    var z = Current.z;
                    
                    z++;
                    
                    if (z == _maxPosition.z)
                    {
                        z = _minPosition.z;
                        y++;
                    }
                    if (y == _maxPosition.y)
                    {
                        y = _minPosition.y;
                        x++;
                    }
                    if (x == _maxPosition.x)
                    {
                        return false;
                    }
                    
                    Current = new Vector3Int(x, y, z);
                    return true;
                }
                
                
                public void Reset()
                {
                    throw new NotImplementedException();
                }
                
                public Vector3Int Current { get; set; }
                
                object IEnumerator.Current => Current;
            }
        }
    }
}