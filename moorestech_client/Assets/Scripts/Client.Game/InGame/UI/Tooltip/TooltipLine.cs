using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     カーソルツールチップの1行。辞書キーと{p0}位置パラメータのみを運び、生の表示文字列は持たない
    ///     One cursor-tooltip line carrying only a dictionary key and {p0} positional params, never raw display text
    /// </summary>
    public readonly struct TooltipLine : IEquatable<TooltipLine>
    {
        public readonly string TextKey;
        public readonly IReadOnlyList<string> TextParams;

        public TooltipLine(LocalizationKey key, IReadOnlyList<string> textParams)
        {
            TextKey = key.Key;
            TextParams = textParams;
        }

        public TooltipLine(LocalizationKey key) : this(key, Array.Empty<string>())
        {
        }

        public bool Equals(TooltipLine other)
        {
            return TextKey == other.TextKey && TextParams.SequenceEqual(other.TextParams);
        }

        public override bool Equals(object obj)
        {
            return obj is TooltipLine other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(TextKey, TextParams.Count);
            foreach (var textParam in TextParams) hash = HashCode.Combine(hash, textParam);
            return hash;
        }
    }
}
