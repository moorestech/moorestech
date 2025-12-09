using Client.Game.InGame.Block;
using VContainer;

namespace Client.Game.InGame.Context
{
    public class ClientDIContext
    {
        public static DIContainer DIContainer { get; private set; }
        public static BlockGameObjectDataStore BlockGameObjectDataStore { get; set; }
        
        public ClientDIContext(DIContainer diContainer)
        {
            DIContainer = diContainer;
            BlockGameObjectDataStore = diContainer.DIContainerResolver.Resolve<BlockGameObjectDataStore>();
        }
    }
}