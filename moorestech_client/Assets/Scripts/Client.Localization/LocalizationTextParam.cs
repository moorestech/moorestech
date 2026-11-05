using System;

namespace Client.Localization
{
    // ・{Name}をValueで置換
    // Replaces {Name} with Value
    public readonly struct LocalizationTextParam : IEquatable<LocalizationTextParam>
    {
        public readonly string Name;
        public readonly string Value;

        public LocalizationTextParam(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public bool Equals(LocalizationTextParam other)
        {
            return Name == other.Name && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizationTextParam other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Value);
        }
    }
}
