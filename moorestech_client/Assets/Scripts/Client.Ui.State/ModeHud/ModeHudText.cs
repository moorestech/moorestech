using System;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// HUD の有るかもしれない文（無しは HasValue）
    /// An optional HUD text (absence is HasValue)
    /// </summary>
    public readonly struct ModeHudText : IEquatable<ModeHudText>
    {
        public static readonly ModeHudText None = default;

        public readonly bool HasValue;
        public readonly string Value;

        private ModeHudText(string value)
        {
            HasValue = true;
            Value = value;
        }

        public static ModeHudText Of(string value) => new(value);
        public bool Equals(ModeHudText other) => HasValue == other.HasValue && Value == other.Value;
        public override bool Equals(object obj) => obj is ModeHudText other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(HasValue, Value);
    }
}
