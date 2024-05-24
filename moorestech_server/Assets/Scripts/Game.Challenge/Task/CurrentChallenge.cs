using System;
using UniRx;

namespace Game.Challenge.Task
{
    public class CurrentChallenge
    {
        public IObservable<ChallengeInfo> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<ChallengeInfo> _onChallengeComplete = new();

        public readonly ChallengeInfo Config;
        public readonly int Id;

        public CurrentChallenge(ChallengeInfo config, int id)
        {
            Config = config;
            Id = id;
        }
    }
}