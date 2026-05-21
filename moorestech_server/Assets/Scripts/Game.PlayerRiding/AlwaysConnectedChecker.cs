using Game.PlayerRiding.Interface;

namespace Game.PlayerRiding
{
    // IPlayerConnectionChecker の暫定実装。Phase 3 で実接続レジストリに差し替える。
    // Stub IPlayerConnectionChecker. Phase 3 replaces it with the real connection registry.
    public class AlwaysConnectedChecker : IPlayerConnectionChecker
    {
        public bool IsConnected(int playerId) => true;
    }
}
