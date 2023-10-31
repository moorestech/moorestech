using System;
using UnityEngine;

namespace MainGame.Basic
{
    public enum BlockDirection
    {
        North,
        East,
        South,
        West
    }
    
    public static class BlockDirectionAngle
    {
        public static Quaternion GetRotation(BlockDirection direction)
        {
            switch (direction)
            {
                case BlockDirection.North:
                    return Quaternion.Euler(0, 0, 0);
                case BlockDirection.East:
                    return Quaternion.Euler(0, 90, 0);
                case BlockDirection.South:
                    return Quaternion.Euler(0, 180, 0);
                case BlockDirection.West:
                    return Quaternion.Euler(0, 270, 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}