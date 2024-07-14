using System;

namespace Game.Challenge.Task
{
    public interface IChallengeTask
    {
        public ChallengeInfo Config { get; }
        public int PlayerId { get; }
        
        public IObservable<IChallengeTask> OnChallengeComplete { get; }
        
        public void ManualUpdate();
    }
}