using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Tooltip;
using Client.Ui.State.Text;

namespace Client.Ui.State.CursorTooltip
{
    /// <summary>
    /// カーソルツールチップの表示状態。Web の topic ui.tooltip の写しで、行が空なら隠す
    /// Display state of the cursor tooltip; a copy of Web's ui.tooltip topic, hidden when there are no lines
    /// </summary>
    public sealed class CursorTooltipViewModel : IEquatable<CursorTooltipViewModel>
    {
        public static readonly CursorTooltipViewModel Hidden = new(Array.Empty<TooltipLine>());

        public readonly IReadOnlyList<UiText> Lines;
        public readonly IReadOnlyList<TooltipLine> SourceLines;
        public bool Visible => 0 < Lines.Count;

        public CursorTooltipViewModel(IReadOnlyList<TooltipLine> sourceLines)
        {
            SourceLines = sourceLines;
            Lines = sourceLines.Select(line => new UiText(line.Key, line.TextParams)).ToArray();
        }

        // UiText は参照で比べるので、元の行（値の等価を持つ TooltipLine）で比べる
        // UiText compares by reference, so compare the source lines, which have value equality
        public bool Equals(CursorTooltipViewModel other)
        {
            return other != null && SourceLines.SequenceEqual(other.SourceLines);
        }

        public override bool Equals(object obj) => Equals(obj as CursorTooltipViewModel);
        public override int GetHashCode() => SourceLines.Count;
    }
}
