using System;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // 機械とベルコンのblockが接する面の鍵。箱型の機械が1つのベルコンマスに接する面は1つなので、この組が面と1対1になる
    // Key of the face where a machine and a belt block touch; a box-shaped machine touches one belt cell on exactly one face, so this pair identifies the face
    public readonly struct BeltMachineSupplyKey : IEquatable<BeltMachineSupplyKey>
    {
        public readonly BlockInstanceId BeltBlockInstanceId;
        public readonly BlockInstanceId MachineBlockInstanceId;

        public BeltMachineSupplyKey(BlockInstanceId beltBlockInstanceId, BlockInstanceId machineBlockInstanceId)
        {
            BeltBlockInstanceId = beltBlockInstanceId;
            MachineBlockInstanceId = machineBlockInstanceId;
        }

        public bool Equals(BeltMachineSupplyKey other)
        {
            return BeltBlockInstanceId == other.BeltBlockInstanceId && MachineBlockInstanceId == other.MachineBlockInstanceId;
        }

        public override bool Equals(object obj)
        {
            return obj is BeltMachineSupplyKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BeltBlockInstanceId, MachineBlockInstanceId);
        }
    }
}
