using System;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// 操作モード HUD の「有るかもしれない文」。無いことを空文字ではなく HasValue で表す
    /// An optional text of the operation-mode HUD; absence is HasValue, not an empty string
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
