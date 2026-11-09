namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// 1 つのアンカーの解決結果。ready だけが矩形と祖先クリップを持ち、未解決は理由だけを持つ
    /// One anchor's resolution; only Ready carries the rect and ancestor clip, unresolved states carry only a reason
    /// </summary>
    public abstract class TutorialAnchorState
    {
        public readonly TutorialAnchorReason Reason;

        private TutorialAnchorState(TutorialAnchorReason reason)
        {
            Reason = reason;
        }

        // ready は矩形と clip まで、未解決は種と理由まで比べる（Web の isSameAnchor）
        // Ready compares rect and clip, unresolved states compare kind and reason (Web isSameAnchor)
        public abstract bool SameAs(TutorialAnchorState other);

        public sealed class Ready : TutorialAnchorState
        {
            public readonly TutorialClipRect Rect;
            public readonly TutorialClipRect Clip;

            public Ready(TutorialClipRect rect, TutorialClipRect clip) : base(TutorialAnchorReason.Mounted)
            {
                Rect = rect;
                Clip = clip;
            }

            public override bool SameAs(TutorialAnchorState other)
            {
                return other is Ready ready && ready.Rect.Equals(Rect) && ready.Clip.Equals(Clip);
            }
        }

        public sealed class NotFound : TutorialAnchorState
        {
            // 種と理由の食い違いを作らせないため、生成は理由ごとの口に限る
            // Construction goes only through per-reason factories so kind and reason can never disagree
            public static NotFound Missing() => new(TutorialAnchorReason.Missing);
            public static NotFound DuplicateAnchor() => new(TutorialAnchorReason.DuplicateAnchor);

            private NotFound(TutorialAnchorReason reason) : base(reason) { }

            public override bool SameAs(TutorialAnchorState other)
            {
                return other is NotFound && other.Reason == Reason;
            }
        }

        public sealed class Hidden : TutorialAnchorState
        {
            public static Hidden DisplayNone() => new(TutorialAnchorReason.DisplayNone);
            public static Hidden VisibilityHidden() => new(TutorialAnchorReason.VisibilityHidden);
            public static Hidden ZeroArea() => new(TutorialAnchorReason.ZeroArea);
            public static Hidden OutsideViewport() => new(TutorialAnchorReason.OutsideViewport);

            private Hidden(TutorialAnchorReason reason) : base(reason) { }

            public override bool SameAs(TutorialAnchorState other)
            {
                return other is Hidden && other.Reason == Reason;
            }
        }
    }
}
