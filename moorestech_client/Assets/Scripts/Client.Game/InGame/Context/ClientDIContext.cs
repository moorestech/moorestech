using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.Notification;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using VContainer;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;

namespace Client.Game.InGame.Context
{
    public class ClientDIContext
    {
        public static DIContainer DIContainer { get; private set; }
        public static BlockGameObjectDataStore BlockGameObjectDataStore { get; set; }
        public static BuildOperationHistory BuildOperationHistory { get; private set; }

        public static BlockAttachedConnectionResolver BlockAttachedConnectionResolver { get; private set; }
        public static ConnectionLineRegistry ConnectionLineRegistry { get; private set; }
        public static ClientLocalNotificationSource ClientLocalNotificationSource { get; private set; }
        public static IBlueprintThumbnailLookup BlueprintThumbnailLookup { get; private set; }

        public ClientDIContext(DIContainer diContainer)
        {
            DIContainer = diContainer;
            ClientLocalNotificationSource = diContainer.DIContainerResolver.Resolve<ClientLocalNotificationSource>();
            ConnectionLineRegistry = diContainer.DIContainerResolver.Resolve<ConnectionLineRegistry>();
            BlockGameObjectDataStore = diContainer.DIContainerResolver.Resolve<BlockGameObjectDataStore>();
            BlockAttachedConnectionResolver = diContainer.DIContainerResolver.Resolve<BlockAttachedConnectionResolver>();
            BuildOperationHistory = diContainer.DIContainerResolver.Resolve<BuildOperationHistory>();
            BlueprintThumbnailLookup = diContainer.DIContainerResolver.Resolve<IBlueprintThumbnailLookup>();
        }
    }
}
