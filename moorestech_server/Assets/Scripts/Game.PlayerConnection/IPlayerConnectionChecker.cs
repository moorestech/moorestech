namespace Game.PlayerConnection
{
    // プレイヤーが接続中かを判定する抽象。乗車システムの座席占有判定などが「接続中プレイヤーのみ」を対象にするため必要。
    // Phase 2 では常に true を返す暫定実装を使い、Phase 3 で実接続レジストリに差し替える。
    // Abstraction for "is this player connected". Phase 3 replaces the stub implementation.
    public interface IPlayerConnectionChecker
    {
        bool IsConnected(int playerId);
    }
}
