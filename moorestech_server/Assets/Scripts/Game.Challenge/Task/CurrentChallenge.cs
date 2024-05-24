using System;
using UniRx;

namespace Game.Challenge.Task
{
    public class CurrentChallenge
    {
        public IObservable<CurrentChallenge> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<CurrentChallenge> _onChallengeComplete = new();

        public readonly ChallengeInfo Config;
        public readonly int PlayerId;

        public void ManualUpdate()
        {
            
        }

        public CurrentChallenge(int playerId, ChallengeInfo config)
        {
            Config = config;
            PlayerId = playerId;
        }
    }
}