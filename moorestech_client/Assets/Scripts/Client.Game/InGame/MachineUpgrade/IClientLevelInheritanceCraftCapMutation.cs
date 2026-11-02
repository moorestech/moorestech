using System;

namespace Client.Game.InGame.MachineUpgrade
{
    public interface IClientLevelInheritanceCraftCapMutation
    {
        void Apply(Guid familyBaseItemGuid, int level);
    }
}
