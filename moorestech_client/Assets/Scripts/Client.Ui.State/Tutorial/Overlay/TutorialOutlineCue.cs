using Client.Ui.State.Text;

namespace Client.Ui.State.Tutorial.Overlay
{
    /// <summary>
    /// 強調枠 1 つ分の指示。ラベル無し（Plain）とあり（Labeled）の判別 union
    /// One outline cue: a discriminated union of Plain (no label) and Labeled
    /// </summary>
    public abstract class TutorialOutlineCue
    {
        public readonly string Key;
        public readonly string AnchorId;
        public readonly int PaddingPx;

        private TutorialOutlineCue(string sessionId, string elementId, string anchorId, int paddingPx)
        {
            Key = TutorialElementKey.Of(sessionId, elementId);
            AnchorId = anchorId;
            PaddingPx = paddingPx;
        }

        public sealed class Plain : TutorialOutlineCue
        {
            public Plain(string sessionId, string elementId, string anchorId, int paddingPx) : base(sessionId, elementId, anchorId, paddingPx)
            {
            }
        }

        public sealed class Labeled : TutorialOutlineCue
        {
            public readonly UiText Label;

            public Labeled(string sessionId, string elementId, string anchorId, int paddingPx, UiText label) : base(sessionId, elementId, anchorId, paddingPx)
            {
                Label = label;
            }
        }
    }
}
