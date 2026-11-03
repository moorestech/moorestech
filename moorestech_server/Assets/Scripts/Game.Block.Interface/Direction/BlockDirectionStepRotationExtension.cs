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

        // 上下成分を落として同じ水平方位へ寝かせる
        // Drop the up/down component and lay the direction flat on the same heading
        public static BlockDirection ToHorizontalDirection(this BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.UpNorth or BlockDirection.DownNorth => BlockDirection.North,
                BlockDirection.UpEast or BlockDirection.DownEast => BlockDirection.East,
                BlockDirection.UpSouth or BlockDirection.DownSouth => BlockDirection.South,
                BlockDirection.UpWest or BlockDirection.DownWest => BlockDirection.West,
                _ => blockDirection
            };
        }
    }
}
