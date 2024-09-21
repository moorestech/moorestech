using System;
using Mooresmaster.Model.ChallengesModule;

namespace Game.Challenge.Task
{
    public interface IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public int PlayerId { get; }
        
        public IObservable<IChallengeTask> OnChallengeComplete { get; }
        
        public void ManualUpdate();
    }
}