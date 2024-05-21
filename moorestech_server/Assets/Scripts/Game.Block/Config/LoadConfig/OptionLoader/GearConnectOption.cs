using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.OptionLoader
{
    public class GearConnectOption : IConnectOption
    {
        public bool IsReverse;
        
        public GearConnectOption(bool isReverse)
        {
            IsReverse = isReverse;
        }
        
        public static IConnectOption Loader(dynamic connectorOption)
        {
            var reverse = (bool)connectorOption.isReverse;
            
            return new GearConnectOption(reverse);
        }
    }
}