using Mooresmaster.Model.ChallengeActionModule;

namespace Game.Action
{
    public interface IGameActionExecutor
    {
        void ExecuteUnlockActions(ChallengeActionElement[] actions, ActionExecutionContext context = default);
        void ExecuteActions(ChallengeActionElement[] actions, ActionExecutionContext context = default);
    }
}
