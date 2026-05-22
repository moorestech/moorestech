using System;

namespace Game.PlayerRiding.Interface
{
    // 乗車状態1件のセーブDTO。識別子は RidableType（判別子）と型別ペイロード文字列で保存する
    // Save DTO for one riding state. Identified by RidableType (discriminator) and type-specific payload string.
    [Serializable]
    public class PlayerRidingSaveData
    {
        public int PlayerId { get; }

        public string RidableType { get; }
        public string IdentifierState { get; }

        public int SeatIndex { get; }

        public PlayerRidingSaveData(int playerId, string ridableType, string identifierState, int seatIndex)
        {
            PlayerId = playerId;
            RidableType = ridableType;
            IdentifierState = identifierState;
            SeatIndex = seatIndex;
        }
    }
}
