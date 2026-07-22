using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using VContainer;

namespace Client.Game.InGame.Context
{
    public class ClientDIContext
    {
        public static DIContainer DIContainer { get; private set; }
        public static BlockGameObjectDataStore BlockGameObjectDataStore { get; set; }
        public static BuildOperationHistory BuildOperationHistory { get; private set; }

        public ClientDIContext(DIContainer diContainer)
        {
            DIContainer = diContainer;
            BlockGameObjectDataStore = diContainer.DIContainerResolver.Resolve<BlockGameObjectDataStore>();
            BuildOperationHistory = diContainer.DIContainerResolver.Resolve<BuildOperationHistory>();
        }
    }
}