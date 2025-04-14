
using Client.Game.InGame.UI.Util;

namespace Client.Game.InGame.Mining
{
    public class MapObjectMiningIdleState : IMapObjectMiningState
    {
        public MapObjectMiningIdleState()
        {
            MouseCursorTooltip.Instance.Hide();
        }
        
        public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
        {
            return
                context.CurrentFocusMapObjectGameObject != null 
                    ? new MapObjectMiningFocusState() 
                    : this;
        }
    }
}