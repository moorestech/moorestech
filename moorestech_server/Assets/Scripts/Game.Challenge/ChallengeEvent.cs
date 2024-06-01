using System;
using Game.Challenge.Task;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeEvent
    {
        private readonly Subject<CurrentChallenge> _onCompleteChallenge = new();
        public IObservable<CurrentChallenge> OnCompleteChallenge => _onCompleteChallenge;
        
        public void InvokeCompleteChallenge(CurrentChallenge craftConfig)
        {
            _onCompleteChallenge.OnNext(craftConfig);
        }
    }
}