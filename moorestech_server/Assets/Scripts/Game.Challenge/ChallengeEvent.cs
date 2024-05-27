using System;
using Game.Challenge.Task;
using Game.Crafting.Interface;
using UniRx;

namespace Game.Challenge
{
    public class ChallengeEvent
    {
        public IObservable<CurrentChallenge> OnCompleteChallenge => _onCompleteChallenge;
        private readonly Subject<CurrentChallenge> _onCompleteChallenge = new();

        public void InvokeCompleteChallenge(CurrentChallenge craftConfig)
        {
            _onCompleteChallenge.OnNext(craftConfig);
        }
    }
}