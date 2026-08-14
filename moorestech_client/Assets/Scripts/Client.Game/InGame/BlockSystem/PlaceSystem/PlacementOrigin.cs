using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    ///     設置対象がどこから選ばれたかの由来。設置対象と同じトランザクションで所有者へ渡す
    ///     Where a placement target was chosen from; handed to the owner in the same transaction as the target
    /// </summary>
    public readonly struct PlacementOrigin : IEquatable<PlacementOrigin>
    {
        // 設置対象を保持していない状態の由来
        // The origin held while nothing is being placed
        public static readonly PlacementOrigin None = new(OriginKind.None, default);
        public static readonly PlacementOrigin Menu = new(OriginKind.Menu, default);
        public static readonly PlacementOrigin Eyedropper = new(OriginKind.Eyedropper, default);

        public static PlacementOrigin FromHotbarSlot(int slot)
        {
            return new PlacementOrigin(OriginKind.HotbarSlot, slot);
        }

        private readonly OriginKind _kind;
        private readonly int _hotbarSlot;

        private PlacementOrigin(OriginKind kind, int hotbarSlot)
        {
            _kind = kind;
            _hotbarSlot = hotbarSlot;
        }

        // ホットバー由来のときだけ枠番号を渡す。他の由来は枠を持たない
        // Yields the slot index only for a hotbar origin; the other origins hold no slot
        public bool TryGetHotbarSlot(out int slot)
        {
            slot = _hotbarSlot;
            return _kind == OriginKind.HotbarSlot;
        }

        public bool Equals(PlacementOrigin other)
        {
            return _kind == other._kind && _hotbarSlot == other._hotbarSlot;
        }

        public override bool Equals(object obj)
        {
            return obj is PlacementOrigin other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)_kind, _hotbarSlot);
        }

        // 由来の語彙はこの値の内側に閉じ、設置システム側へは漏らさない
        // The origin vocabulary stays inside this value and never leaks into the placement system
        private enum OriginKind
        {
            None,
            Menu,
            Eyedropper,
            HotbarSlot,
        }
    }
}
