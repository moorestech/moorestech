using System.Collections.Generic;

namespace Game.PlayerRiding.Interface
{
    // 乗車状態データストアの契約。乗車操作・降車・破棄・ログイン復帰・永続化を提供する。
    // 利用側（プロトコル・セーブシステム）はこの抽象に依存し、具象 PlayerRidingDatastore には依存しない。
    // Contract for the riding-state datastore. Callers depend on this abstraction, not the concrete class.
    public interface IPlayerRidingDatastore
    {
        bool TryGetRidingState(int playerId, out RidingState ridingState);
        RideActionResult TryRide(int playerId, IRidableIdentifier identifier, out int assignedSeatIndex);
        RideActionResult TryDismount(int playerId);
        IReadOnlyList<int> OnRidableRemoved(IRidableIdentifier identifier);
        bool EvaluateOnLogin(int playerId);

        // 永続化。セーブシステムが呼ぶ（仕様書セクション10）。
        // Persistence, called by the save system.
        List<PlayerRidingSaveData> GetSaveData();
        void LoadSaveData(IReadOnlyList<PlayerRidingSaveData> saveData);
    }
}
