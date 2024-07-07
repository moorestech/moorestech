using System.Collections.Generic;

namespace Game.Challenge
{
    public interface IChallengeConfig
    {
        public IReadOnlyList<ChallengeInfo> InitialChallenges { get; }
        public ChallengeInfo GetChallenge(int playerId);
    }
}