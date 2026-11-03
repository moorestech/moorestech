namespace Game.Block.Interface
{
    /// <summary>
    ///     - 設置時の方向切替
    ///     - 水平に90度ずつ回し、上下成分は保つ
    ///     - Direction cycling during placement
    ///     - Horizontal 90-degree steps that keep the up/down component
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
    }
}
