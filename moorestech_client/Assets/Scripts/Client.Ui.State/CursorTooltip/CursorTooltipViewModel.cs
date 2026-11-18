using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Tooltip;
using Client.Ui.State.Text;

namespace Client.Ui.State.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの表示状態
    /// Display state of the cursor tooltip
    /// </summary>
    public sealed class CursorTooltipViewModel : IEquatable<CursorTooltipViewModel>
    {
        public static readonly CursorTooltipViewModel Hidden = new(Array.Empty<TooltipLine>());

        public readonly IReadOnlyList<UiText> Lines;
        private readonly IReadOnlyList<TooltipLine> _sourceLines;
        public bool Visible => 0 < Lines.Count;

        public CursorTooltipViewModel(IReadOnlyList<TooltipLine> sourceLines)
        {
            _sourceLines = sourceLines;
            Lines = sourceLines.Select(line => new UiText(line.Key, line.TextParams)).ToArray();
        }

        // UiText は参照比較なので TooltipLine で比べる
        // UiText compares by reference, so compare TooltipLine
        public bool Equals(CursorTooltipViewModel other)
        {
            return other != null && _sourceLines.SequenceEqual(other._sourceLines);
        }

        public override bool Equals(object obj) => Equals(obj as CursorTooltipViewModel);
        public override int GetHashCode() => _sourceLines.Count;
    }
}
