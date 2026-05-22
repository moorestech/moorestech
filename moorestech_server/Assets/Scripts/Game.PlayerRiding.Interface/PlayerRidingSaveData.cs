using System;

namespace Game.PlayerRiding.Interface
{
    // 乗車状態1件のセーブDTO。識別子は RidableType（判別子）と型別ペイロード文字列で保存する（仕様書セクション10）。
    // 乗り物種別が増えても DTO は変えず、各 IRidableIdentifier の GetSaveState() がペイロードを担う。
    // Save DTO for one riding state. The identifier is stored as a RidableType discriminator plus a per-type payload string.
    [Serializable]
    public class PlayerRidingSaveData
    {
        public int PlayerId { get; set; }
        // 乗り物種別の判別子（RidableType の primitive 文字列）
        // Discriminator of the ridable type (the RidableType primitive string).
        public string RidableType { get; set; }
        // RidableType ごとの識別子ペイロード（IRidableIdentifier.GetSaveState() が返す文字列）
        // Per-type identifier payload produced by IRidableIdentifier.GetSaveState().
        public string IdentifierState { get; set; }
        public int SeatIndex { get; set; }
    }
}
