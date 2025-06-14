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
        Client.Network.API.ChallengeResponse ChallengeData { get; }
    }

    public interface IReadOnlyCraftTreeState
    {
        Client.Network.API.CraftTreeResponse CraftTreeData { get; }
    }
}