namespace Client.Game.InGame.Tutorial
{
    public class TutorialPresentationData
    {
        public string TutorialSessionId;
        public int Revision;
        public string ChallengeId;
        public TutorialHighlightData[] Highlights;
        public TutorialDragGuideData[] DragGuides;
    }

    public class TutorialHighlightData
    {
        public string HighlightId;
        public string AnchorId;
        public string Kind;
        public int PaddingPx;
        public bool BlocksPointerInput;
    }

    public class TutorialDragGuideData
    {
        public string GuideId;
        public string FromAnchorId;
        public string ToAnchorId;
    }
}
