using UnityEngine;

namespace Game.Block.Interface
{
    public class BlockPositionInfo
    {
        /// <summary>
        ///     オリジナル座標は常に左下（ブロックが専有する範囲の最小の座標）になる
        /// </summary>
        public Vector3Int OriginalPos { get; }
        public Vector3Int BlockSize { get; }
        
        public Vector3Int MinPos => OriginalPos;
        public Vector3Int MaxPos { get; }
        
        public BlockDirection BlockDirection { get; }
        
        public BlockPositionInfo(Vector3Int originalPos, BlockDirection blockDirection, Vector3Int blockSize)
        {
            OriginalPos = originalPos;
            BlockDirection = blockDirection;
            BlockSize = blockSize;
            
            MaxPos = CalcBlockMaxPos(originalPos, blockDirection, BlockSize);
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
    }
}