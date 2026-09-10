using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     表示内容が同じなら同値として扱い、毎フレーム作り直される配列で変化通知が湧かないようにする
    ///     Equal content compares equal, so the array rebuilt every frame never raises a change notification
    /// </summary>
    public readonly struct TooltipPresentation : IEquatable<TooltipPresentation>
    {
        public static readonly TooltipPresentation Hidden = new(Array.Empty<TooltipLine>());

        public readonly IReadOnlyList<TooltipLine> Lines;

        // 表示状態は行から導出する（行が無いのに表示中という矛盾した状態を作らせない）
        // Visibility is derived from the lines, so a contradictory "visible with no lines" state cannot exist
        public bool Visible => 0 < Lines.Count;

        public TooltipPresentation(IReadOnlyList<TooltipLine> lines)
        {
            Lines = lines;
        }

        public bool Equals(TooltipPresentation other)
        {
            return Lines.SequenceEqual(other.Lines);
        }

        public override bool Equals(object obj)
        {
            return obj is TooltipPresentation other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = Lines.Count;
            foreach (var line in Lines) hash = HashCode.Combine(hash, line);
            return hash;
        }
    }
}
