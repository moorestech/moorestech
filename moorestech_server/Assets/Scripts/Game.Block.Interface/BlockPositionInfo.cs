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
        
        /// <summary>
        ///     ブロックローカル座標のセルが、回転と設置位置を加味してワールドのどのセルになるかを返す
        ///     Returns the world cell a block-local cell lands on, accounting for the block's rotation and position
        /// </summary>
        public Vector3Int ConvertBlockLocalToWorldCell(Vector3Int blockLocalCell)
        {
            // コネクターのoffsetと同じ換算を共通の回転関数へ委譲し、原点だけここで足す
            // Delegates the same conversion as connector offsets to the shared rotation helper, adding only the origin here
            return BlockDirection.GetBlockBaseOriginPos(this) + BlockDirection.ConvertLocalCell(blockLocalCell);
        }
        
        /// <summary>
        ///     サーバー側管理のブロックの最大座標を計算する
        ///     これはどのグリッドにブロックが存在しているかということに使われるため、サイズ 1,1 の場合、originとmaxの値はおなじになる
        /// </summary>
        public static Vector3Int CalcBlockMaxPos(Vector3Int originPos, BlockDirection direction, Vector3Int blockSize)
        {
            var addPos = direction switch
            {
                BlockDirection.North or BlockDirection.South => new Vector3Int(blockSize.x, blockSize.y, blockSize.z),
                BlockDirection.East or BlockDirection.West => new Vector3Int(blockSize.z, blockSize.y, blockSize.x),
                _ => throw new System.ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
            
            // block sizeは1からとなっているが、ここで求めるのはブロックが占める範囲の最大値なので、-1している
            return addPos + originPos - Vector3Int.one;
        }
    }
}