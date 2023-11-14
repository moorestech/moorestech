using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public class WorldBlockData
    {
        public WorldBlockData(IBlock block, int originX, int originY, BlockDirection blockDirection,
            IBlockConfig blockConfig)
        {
            OriginX = originX;
            OriginY = originY;
            BlockDirection = blockDirection;
            Block = block;
            var config = blockConfig.GetBlockConfig(block.BlockId);
            Height = config.BlockSize.y;
            Width = config.BlockSize.x;
            
            var maxPos = CalcBlockGridMaxPos(new Vector2Int(originX, originY), blockDirection, config.BlockSize);
            MaxX = maxPos.x;
            MaxY = maxPos.y;
        }

        public int OriginX { get; }
        public int OriginY { get; }
        public int Height { get; }
        public int Width { get; }


        public int MaxX { get; }

        public int MaxY { get; }

        public IBlock Block { get; }
        public BlockDirection BlockDirection { get; }

        public bool IsContain(int x, int y)
        {
            return OriginX <= x && x <= MaxX && OriginY <= y && y <= MaxY;
        }


        /// <summary>
        /// サーバー側管理のブロックの最大座標を計算する
        /// これはどのグリッドにブロックが存在しているかということに使われるため、サイズ 1,1 の場合、originとmaxの値はおなじになる
        /// TODO これは命名も含めて修正したほうが良いかもしれない
        /// </summary>
        public static Vector2Int CalcBlockGridMaxPos(Vector2Int originPos,BlockDirection direction,Vector2Int blockSize)
        {
            var maxX = (direction is BlockDirection.North or BlockDirection.South
                ? originPos.x + blockSize.x
                : originPos.x + blockSize.y) - 1;
            var maxY = (direction is BlockDirection.North or BlockDirection.South
                ? originPos.y + blockSize.y
                : originPos.y + blockSize.x) - 1;

            return new Vector2Int(maxX, maxY);
        }
    }
    
}