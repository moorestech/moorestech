using System;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Components.Controls
{
    public enum ModeSwitchOrientation
    {
        Horizontal,
        Vertical,
    }

    /// <summary>
    /// 選択肢1つ（キーか生文字列）
    /// One option (key or raw string)
    /// </summary>
    public sealed class ModeSwitchOption<TValue>
    {
        public string Testid { get; }
        public TValue Value { get; }
        public LocalizationKey? LabelKey { get; }
        public string LiteralLabel { get; }

        public ModeSwitchOption(string testid, TValue value, LocalizationKey? labelKey, string literalLabel)
        {
            // 出どころ不定は即停止
            // Stop when the source is not exactly one
            var hasLiteral = literalLabel != null;
            if (labelKey.HasValue == hasLiteral) throw new ArgumentException($"[ModeSwitchOption] {testid} needs exactly one of labelKey or literalLabel");

            Testid = testid;
            Value = value;
            LabelKey = labelKey;
            LiteralLabel = literalLabel;
        }
    }
}
