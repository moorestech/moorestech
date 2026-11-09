using System;

namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// 画面座標の矩形（左上原点・パネル単位）。Web の ClipRect の写しで、値で比べる
    /// A screen rect (top-left origin, panel units); a copy of Web's ClipRect compared by value
    /// </summary>
    public readonly struct TutorialClipRect : IEquatable<TutorialClipRect>
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;

        public TutorialClipRect(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public float Width => Right - Left;
        public float Height => Bottom - Top;
        public float CenterX => (Left + Right) / 2f;
        public float CenterY => (Top + Bottom) / 2f;

        public TutorialClipRect Intersect(TutorialClipRect other)
        {
            return new TutorialClipRect(Math.Max(Left, other.Left), Math.Max(Top, other.Top), Math.Min(Right, other.Right), Math.Min(Bottom, other.Bottom));
        }

        public TutorialClipRect Expand(float amount)
        {
            return new TutorialClipRect(Left - amount, Top - amount, Right + amount, Bottom + amount);
        }

        // Web の isDisjoint と同じく辺が接するだけでも交わらないとみなす
        // Like Web's isDisjoint, touching edges count as disjoint
        public bool IsDisjoint(TutorialClipRect other)
        {
            return other.Right <= Left || Right <= other.Left || other.Bottom <= Top || Bottom <= other.Top;
        }

        public bool Contains(float x, float y)
        {
            return Left <= x && x <= Right && Top <= y && y <= Bottom;
        }

        public bool ContainsRect(TutorialClipRect inner)
        {
            return Left <= inner.Left && inner.Right <= Right && Top <= inner.Top && inner.Bottom <= Bottom;
        }

        public bool Equals(TutorialClipRect other)
        {
            return Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        }

        public override bool Equals(object obj)
        {
            return obj is TutorialClipRect other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Left, Top, Right, Bottom);
        }
    }
}
