namespace Game.Block.Interface.BlockConfig
{
    public interface IConnectOptionLoader
    {
        public IConnectOption Load(dynamic connectorOption);
    }
}