using Mooresmaster.Model.ChallengeActionModule;

namespace Game.Action
{
    public interface IGameActionExecutor
    {
        void ExecuteAction(ChallengeActionElement action);
    }
}