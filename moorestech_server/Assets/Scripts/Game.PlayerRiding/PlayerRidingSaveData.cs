using System;

namespace Game.PlayerRiding
{
    // 乗車状態1件のセーブDTO。識別子は RidableType + 型別ペイロードで保存する（仕様書セクション10）。
    // Save DTO for one riding state. The identifier is stored as RidableType + per-type payload.
    [Serializable]
    public class PlayerRidingSaveData
    {
        public int PlayerId { get; set; }
        // Game.PlayerRiding.Interface.RidableType を byte で保存する
        // Stores Game.PlayerRiding.Interface.RidableType as a byte.
        public byte RidableType { get; set; }
        // RidableType==TrainCar のときの車両ID
        // The train car id when RidableType==TrainCar.
        public long TrainCarInstanceId { get; set; }
        public int SeatIndex { get; set; }
    }
}
