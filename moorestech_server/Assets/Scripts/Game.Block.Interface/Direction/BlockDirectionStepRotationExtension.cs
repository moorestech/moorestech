using System;

namespace Game.Block.Interface
{
    /// <summary>
    ///     設置時の方向切替。水平に90度ずつ回す
    ///     Direction cycling during placement; turns 90 degrees horizontally
    /// </summary>
    public static class BlockDirectionStepRotationExtension
    {
        public static BlockDirection HorizonRotation(this BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.North => BlockDirection.East,
                BlockDirection.East => BlockDirection.South,
                BlockDirection.South => BlockDirection.West,
                BlockDirection.West => BlockDirection.North,
                _ => throw new ArgumentOutOfRangeException(nameof(blockDirection), blockDirection, null),
            };
        }
    }
}
