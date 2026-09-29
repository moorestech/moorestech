using System.Collections.Generic;

namespace Game.PlayerConnection
{
    // プレイヤーが接続中かを判定する抽象。乗車システムの座席占有判定などが「接続中プレイヤーのみ」を対象にするため必要。
    // Abstraction for "is this player connected"; used when only online riders should count.
    public interface IPlayerConnectionChecker
    {
        bool IsConnected(int playerId);

        // 接続中の全playerId。区間の開始時点の接続集合を記録し、再生で復元するために要る
        // Every connected playerId; needed to record the connection set at a segment's start and restore it during replay
        IReadOnlyCollection<int> ConnectedPlayerIds();
    }
}
