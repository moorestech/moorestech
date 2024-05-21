using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig
{
    public interface IConnectOptionLoader
    {
        public IConnectOption Load(dynamic connectorOption);
    }
}