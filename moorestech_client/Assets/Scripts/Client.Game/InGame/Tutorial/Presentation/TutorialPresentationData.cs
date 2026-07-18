namespace Client.Game.InGame.Tutorial
{
    public class TutorialPresentationData
    {
        public string TutorialSessionId;
        public int Revision;
        public string ChallengeId;
        public TutorialHighlightData[] Highlights;
    }

    public class TutorialHighlightData
    {
        public string HighlightId;
        public string AnchorId;
        public string Kind;
        public string MessageKey;
        public string Message;
        public int PaddingPx;
        public bool BlocksPointerInput;
    }
}
