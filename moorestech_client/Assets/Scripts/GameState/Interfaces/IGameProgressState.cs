using Game.UnlockState;

namespace GameState
{
    public interface IGameProgressState
    {
        IGameUnlockStateData Unlocks { get; }
        IReadOnlyChallengeState Challenges { get; }
        IReadOnlyCraftTreeState CraftTree { get; }
    }

    public interface IReadOnlyChallengeState
    {
        // TODO: Define challenge state interface based on existing implementation
    }

    public interface IReadOnlyCraftTreeState
    {
        // TODO: Define craft tree state interface based on existing implementation
    }
}