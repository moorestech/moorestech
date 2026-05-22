namespace Game.PlayerRiding.Interface
{
    // プレイヤー1人の乗車状態（プレイヤー ⇄ 乗り物 ⇄ 座席）。
    // One player's riding state (player <-> ridable <-> seat).
    public class RidingState
    {
        public IRidableIdentifier Identifier { get; }
        public int SeatIndex { get; }

        public RidingState(IRidableIdentifier identifier, int seatIndex)
        {
            Identifier = identifier;
            SeatIndex = seatIndex;
        }
    }
}
