using Mooresmaster.Model.ChallengeActionModule;

namespace Game.Challenge
{
    public interface IGameActionExecutor
    {
        void ExecuteAction(ChallengeActionElement action);
    }
}
