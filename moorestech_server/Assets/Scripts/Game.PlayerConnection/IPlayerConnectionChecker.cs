namespace Game.PlayerConnection
{
    // プレイヤーが接続中かを判定する抽象。乗車システムの座席占有判定などが「接続中プレイヤーのみ」を対象にするため必要。
    // Abstraction for "is this player connected"; used when only online riders should count.
    public interface IPlayerConnectionChecker
    {
        bool IsConnected(int playerId);
    }
}
