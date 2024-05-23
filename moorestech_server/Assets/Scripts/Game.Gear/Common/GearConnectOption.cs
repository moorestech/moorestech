using Game.Block.Interface.BlockConfig;

namespace Game.Gear.Common
{
    public class GearConnectOption : IConnectOption
    {
        public readonly bool IsReverse;
        
        public GearConnectOption(bool isReverse)
        {
            IsReverse = isReverse;
        }
    }
    
    public class GearConnectOptionLoader : IConnectOptionLoader
    {
        public static readonly GearConnectOptionLoader Loader = new();
        
        public IConnectOption Load(dynamic connectorOption)
        {
            var reverse = (bool)connectorOption.isReverse;
            return new GearConnectOption(reverse);
        }
    }
}