using System.Collections.Generic;

namespace Game.PlayerRiding.Interface
{
    // 乗車状態データストアのプロトコル向け契約。Phase 3 のプロトコル/イベントはこの抽象に依存する。
    // セーブ/ロード API は含めない（永続化システムは具象 PlayerRidingDatastore を直接使う）。
    // Protocol-facing contract for the riding-state datastore. Phase 3 protocols depend on this abstraction.
    public interface IPlayerRidingDatastore
    {
        bool TryGetRidingState(int playerId, out RidingState ridingState);
        RideActionResult TryRide(int playerId, IRidableIdentifier identifier, out int assignedSeatIndex);
        RideActionResult TryDismount(int playerId);
        IReadOnlyList<int> OnRidableRemoved(IRidableIdentifier identifier);
        bool EvaluateOnLogin(int playerId);
    }
}
