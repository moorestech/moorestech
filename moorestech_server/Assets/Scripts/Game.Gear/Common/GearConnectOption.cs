using Game.Block.Interface.BlockConfig;

namespace Game.Gear.Common
{
    public class GearConnectOption : IConnectOption
    {
        public bool IsReverse;

        public GearConnectOption(bool isReverse)
        {
            IsReverse = isReverse;
        }
    }
}