using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.Inventory.Equipment;

namespace Client.Game.InGame.Mining
{
    public interface IMapObjectMiningState
    {
        IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt);
    }

    public class MapObjectMiningControllerContext
    {
        public MapObjectGameObject CurrentFocusMapObjectGameObject { get; private set; }


        public readonly LocalPlayerEquipment LocalPlayerEquipment;

        public MapObjectMiningControllerContext(LocalPlayerEquipment localPlayerEquipment)
        {
            LocalPlayerEquipment = localPlayerEquipment;
        }
        
        
        
        public void SetFocusMapObjectGameObject(MapObjectGameObject mapObjectGameObject)
        {
            if (mapObjectGameObject != CurrentFocusMapObjectGameObject)
            {
                CurrentFocusMapObjectGameObject?.OnFocus(false);
                mapObjectGameObject?.OnFocus(true);
            }
            
            CurrentFocusMapObjectGameObject = mapObjectGameObject;
        }
    }
}