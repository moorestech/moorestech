using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    ///     設置対象がどこから選ばれたかの由来。設置対象と同じトランザクションで所有者へ渡す
    ///     Where a placement target was chosen from; handed to the owner in the same transaction as the target
    /// </summary>
    public readonly struct PlacementOrigin : IEquatable<PlacementOrigin>
    {
        // ホットバー以外の由来。枠番号を持たない点だけが意味を持つため一値に畳んである
        // Any non-hotbar origin; only "holds no slot" carries meaning, so they collapse into a single value
        public static readonly PlacementOrigin NonHotbar = new(false, default);

        public static PlacementOrigin FromHotbarSlot(int slot)
        {
            return new PlacementOrigin(true, slot);
        }

        private readonly bool _isHotbarSlot;
        private readonly int _hotbarSlot;

        private PlacementOrigin(bool isHotbarSlot, int hotbarSlot)
        {
            _isHotbarSlot = isHotbarSlot;
            _hotbarSlot = hotbarSlot;
        }

        // ホットバー由来のときだけ枠番号を渡す。他の由来は枠を持たない
        // Yields the slot index only for a hotbar origin; the other origins hold no slot
        public bool TryGetHotbarSlot(out int slot)
        {
            slot = _hotbarSlot;
            return _isHotbarSlot;
        }

        public bool Equals(PlacementOrigin other)
        {
            return _isHotbarSlot == other._isHotbarSlot && _hotbarSlot == other._hotbarSlot;
        }

        public override bool Equals(object obj)
        {
            return obj is PlacementOrigin other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_isHotbarSlot, _hotbarSlot);
        }
    }
}
