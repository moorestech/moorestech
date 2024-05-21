using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.OptionLoader
{
    public class GearConnectOptionLoader : IConnectOptionLoader
    {
        public static readonly GearConnectOptionLoader Loader = new();
        
        public IConnectOption Load(dynamic connectorOption)
        {
            var reverse = (bool)connectorOption.isReverse;
            return new GearConnectOption(reverse);
        }
    }
    
    public class GearConnectOption : IConnectOption
    {
        public bool IsReverse;
        
        public GearConnectOption(bool isReverse)
        {
            IsReverse = isReverse;
        }
    }
}