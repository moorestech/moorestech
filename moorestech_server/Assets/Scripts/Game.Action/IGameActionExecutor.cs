using Mooresmaster.Model.GameActionModule;

namespace Game.Action
{
    public interface IGameActionExecutor
    {
        void ExecuteUnlockActions(GameActionElement[] actions, ActionExecutionContext context = default);
        void ExecuteActions(GameActionElement[] actions, ActionExecutionContext context = default);
    }
}
