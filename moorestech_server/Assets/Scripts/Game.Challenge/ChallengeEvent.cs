using System;
using Game.Challenge.Task;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeEvent
    {
        private readonly Subject<IChallengeTask> _onCompleteChallenge = new();
        public IObservable<IChallengeTask> OnCompleteChallenge => _onCompleteChallenge;
        
        public void InvokeCompleteChallenge(IChallengeTask craftConfig)
        {
            _onCompleteChallenge.OnNext(craftConfig);
        }
    }
}