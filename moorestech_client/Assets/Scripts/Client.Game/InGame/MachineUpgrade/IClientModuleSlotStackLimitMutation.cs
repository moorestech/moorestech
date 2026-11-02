using System;

namespace Client.Game.InGame.MachineUpgrade
{
    public interface IClientModuleSlotStackLimitMutation
    {
        void Apply(int level);
    }
}
