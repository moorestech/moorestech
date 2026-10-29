namespace Game.Block.Interface
{
    /// <summary>
    ///     設置操作の1段ぶんの向き送り（水平に90度・上→下→通常の順）
    ///     One step of direction cycling for placement input (90 degrees horizontally, or up/down/normal vertically)
    /// </summary>
    public static class BlockDirectionStepRotationExtension
    {
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
