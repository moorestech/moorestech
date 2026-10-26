using System;

namespace Client.Localization
{
    // 翻訳文の名前付き差し込み1個。{Name}をValueで埋める
    // One named substitution in a translated text; {Name} is replaced by Value
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

        // 録画シナリオのログが名前ごと読めるようにする
        // Keeps recorded-scenario logs readable with the name attached
        public override string ToString()
        {
            return $"{Name}={Value}";
        }
    }
}
