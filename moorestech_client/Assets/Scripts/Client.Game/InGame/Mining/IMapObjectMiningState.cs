using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.Mining
{
    public interface IMapObjectMiningState
    {
        IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt);
    }
    
    public class MapObjectMiningControllerContext
    {
        public MapObjectGameObject CurrentFocusMapObjectGameObject { get; private set; }
        
        
        public readonly HotBarView HotBarView;
        public readonly ILocalPlayerInventory LocalPlayerInventory;
        public readonly IPlayerObjectController PlayerObjectController;
        
        public MapObjectMiningControllerContext(HotBarView hotBarView, ILocalPlayerInventory localPlayerInventory, IPlayerObjectController playerObjectController)
        {
            HotBarView = hotBarView;
            LocalPlayerInventory = localPlayerInventory;
            PlayerObjectController = playerObjectController;
        }
        
        
        
        public void SetFocusMapObjectGameObject(MapObjectGameObject mapObjectGameObject)
        {
            if (mapObjectGameObject != CurrentFocusMapObjectGameObject)
            {
                CurrentFocusMapObjectGameObject?.OutlineEnable(false);
                mapObjectGameObject?.OutlineEnable(true);
            }
            
            CurrentFocusMapObjectGameObject = mapObjectGameObject;
        }
    }
}