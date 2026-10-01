using System;
using Core.BeltTransport;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal static class BeltTransportDirections
    {
        internal static BeltDirection FromVector(Vector3Int vector)
        {
            if (vector.x > 0) return BeltDirection.Right;
            if (vector.x < 0) return BeltDirection.Left;
            if (vector.z > 0) return BeltDirection.Front;
            if (vector.z < 0) return BeltDirection.Back;
            throw new ArgumentException($"Belt direction has no horizontal displacement: {vector}.");
        }
        internal static BeltDirection Forward(BlockPositionInfo position) => FromVector(position.BlockDirection.ConvertLocalCell(Vector3Int.forward));
        internal static BeltDirection Opposite(BeltDirection direction) => (BeltDirection)((int)direction ^ 1);
    }
}
