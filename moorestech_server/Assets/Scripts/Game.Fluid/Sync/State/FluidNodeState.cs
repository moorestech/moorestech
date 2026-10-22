using Core.Master;
using UnityEngine;

namespace Game.Fluid.Sync.State
{
    // 全量に載せるノード(パイプ)1本の、tick境界での位置・容量・中身
    // One node (pipe) in the full state: its position, capacity and contents at the tick boundary
    public readonly struct FluidNodeState
    {
        public readonly Vector3Int Position;
        public readonly double Capacity;
        public readonly double Amount;
        public readonly FluidId FluidId;

        public FluidNodeState(Vector3Int position, double capacity, double amount, FluidId fluidId)
        {
            Position = position;
            Capacity = capacity;
            Amount = amount;
            FluidId = fluidId;
        }
    }
}
