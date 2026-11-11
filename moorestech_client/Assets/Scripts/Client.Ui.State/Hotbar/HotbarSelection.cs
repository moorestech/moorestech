using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// 光らせる枠の有無と番号。Web の selectedSlot -1 を型で表す
    /// Whether a slot is highlighted and which; the Web selectedSlot -1 expressed as a type
    /// </summary>
    public readonly struct HotbarSelection : IEquatable<HotbarSelection>
    {
        public static readonly HotbarSelection None = new(false, 0);

        private readonly bool _hasSlot;
        private readonly int _slotIndex;

        private HotbarSelection(bool hasSlot, int slotIndex)
        {
            _hasSlot = hasSlot;
            _slotIndex = slotIndex;
        }

        private static HotbarSelection Of(int slotIndex)
        {
            return new HotbarSelection(true, slotIndex);
        }

        // 光らせる枠は設置対象の持ち主が持つ由来から導く（表示と実対象が構造的に一致する。Web HotbarTopic と同じ）
        // The highlighted slot derives from the origin the placement owner holds, so display and target agree (same as Web HotbarTopic)
        public static HotbarSelection FromOrigin(PlacementOrigin origin)
        {
            return origin.TryGetHotbarSlot(out var slot) ? Of(slot) : None;
        }

        public bool IsSlot(int slotIndex)
        {
            return _hasSlot && _slotIndex == slotIndex;
        }

        public bool Equals(HotbarSelection other) => _hasSlot == other._hasSlot && _slotIndex == other._slotIndex;
        public override bool Equals(object obj) => obj is HotbarSelection other && Equals(other);
        public override int GetHashCode() => _hasSlot ? _slotIndex + 1 : 0;
    }
}
