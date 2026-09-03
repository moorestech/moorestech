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
            // コネクターのoffsetと同じ換算。デリゲートを作らず行列を直に使い、毎フレーム呼ばれても確保を出さない
            // The same conversion as connector offsets; uses the matrix directly instead of a delegate so per-frame calls allocate nothing
            var rotationMatrix = Matrix4x4.Rotate(BlockDirection.GetRotation());
            var rotated = Vector3Int.RoundToInt(rotationMatrix.MultiplyPoint3x4(blockLocalCell));
            
            return BlockDirection.GetBlockBaseOriginPos(this) + rotated;
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