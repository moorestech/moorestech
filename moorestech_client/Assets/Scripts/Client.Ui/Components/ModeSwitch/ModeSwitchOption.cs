using System;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Components
{
    public enum ModeSwitchOrientation
    {
        Horizontal,
        Vertical,
    }

    /// <summary>
    /// ModeSwitch の選択肢 1 つ。ラベルはローカライズキーか、翻訳しない文字列（言語名・300m 等）のどちらか 1 つ
    /// One ModeSwitch option; its label is either a localization key or an untranslated string (language names, 300m, ...), never both
    /// </summary>
    public sealed class ModeSwitchOption<TValue>
    {
        public string Testid { get; }
        public TValue Value { get; }
        public LocalizationKey? LabelKey { get; }
        public string LiteralLabel { get; }

        public ModeSwitchOption(string testid, TValue value, LocalizationKey? labelKey, string literalLabel)
        {
            // ラベルの出どころが 1 つに決まらない選択肢は作った時点で止める
            // Stop at construction when the label source is not exactly one
            var hasLiteral = literalLabel != null;
            if (labelKey.HasValue == hasLiteral) throw new ArgumentException($"[ModeSwitchOption] {testid} needs exactly one of labelKey or literalLabel");

            Testid = testid;
            Value = value;
            LabelKey = labelKey;
            LiteralLabel = literalLabel;
        }
    }
}
