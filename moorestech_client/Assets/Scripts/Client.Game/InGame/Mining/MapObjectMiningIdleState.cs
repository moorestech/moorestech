
namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningIdleState : IMapObjectMiningState
    {
        public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
        {
            return
                context.CurrentFocusMapObjectGameObject != null 
                    ? new MapObjectMiningFocusState() 
                    : this;
        }
    }
}