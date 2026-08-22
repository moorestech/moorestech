
using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.Mining
{
    public class MiningIdleState : IMiningState
    {
        public MiningIdleState()
        {
            MouseCursorTooltip.Instance.Hide(MiningControllerContext.TooltipOwner);
        }

        public IMiningState GetNextUpdate(MiningControllerContext context, float dt)
        {
            return
                context.CurrentFocusTarget != null
                    ? new MiningFocusState()
                    : this;
        }
    }
}
