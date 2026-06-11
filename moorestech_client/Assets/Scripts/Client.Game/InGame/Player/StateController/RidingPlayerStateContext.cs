using Game.PlayerRiding.Interface;

namespace Client.Game.InGame.Player.StateController
{
    public sealed class RidingPlayerStateContext : IPlayerStateContext
    {
        private readonly RidableIdentifierMessagePack _target;
        private readonly int _seatIndex;

        public RidingPlayerStateContext(RidableIdentifierMessagePack target, int seatIndex)
        {
            // 乗車対象は train/car などの具象種別ではなく ridable 識別子として保持する
            // Keep the riding target as a ridable identifier instead of a concrete train/car type
            _target = target;
            _seatIndex = seatIndex;
        }

        public bool TryGetTarget(out RidableIdentifierMessagePack target)
        {
            target = _target;
            return target != null;
        }

        public int GetSeatIndex()
        {
            return _seatIndex;
        }
    }
}
