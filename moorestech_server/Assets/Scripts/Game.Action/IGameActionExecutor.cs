using System.Collections.Generic;
using Mooresmaster.Model.ChallengeActionModule;

namespace Game.Action
{
    public interface IGameActionExecutor
    {
        void ExecuteUnlockActions(ChallengeActionElement[] actions);
        void ExecuteActions(ChallengeActionElement[] actions);
    }
}