namespace Client.Game.InGame.Mining
{
    public class MiningIdleState : IMiningState
    {
        public MiningIdleState(MiningControllerContext context)
        {
            context.Tooltip.Hide(MiningControllerContext.TooltipOwner);
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
