using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.World.Interface.DataStore
{
    public class WorldBlockData
    {
        public WorldBlockData(IBlock block, int originX, int originY, BlockDirection blockDirection,IBlockConfig blockConfig)
        {
            OriginX = originX;
            OriginY = originY;
            BlockDirection = blockDirection;
            Block = block;
            var config = blockConfig.GetBlockConfig(block.BlockId);
            Height = config.BlockSize.Y;
            Width = config.BlockSize.X;
        }

        public int OriginX { get; }
        public int OriginY { get; }
        public int Height { get; }
        public int Width { get; }
        
        
        public int MaxX => (BlockDirection is BlockDirection.North or BlockDirection.South ? OriginX + Width : OriginX + Height) - 1;
        public int MaxY => (BlockDirection is BlockDirection.North or BlockDirection.South ? OriginY + Height : OriginY + Width) - 1;
        
        public bool IsContain(int x, int y)
        {
            return OriginX <= x && x <= MaxX && OriginY <= y && y <= MaxY;
        }
        
        public IBlock Block { get; }
        public BlockDirection BlockDirection { get; }
    }
}