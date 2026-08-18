namespace Client.Game.InGame.Tutorial
{
    public class TutorialPresentationView : ITutorialView
    {
        private readonly TutorialPresentationStateStore _store;
        private readonly string _sessionId;
        private readonly string _highlightId;

        public TutorialPresentationView(
            TutorialPresentationStateStore store, string sessionId, string highlightId)
        {
            _store = store;
            _sessionId = sessionId;
            _highlightId = highlightId;
        }

        public void CompleteTutorial()
        {
            _store.RemoveHighlight(_sessionId, _highlightId);
        }
    }

    public class TutorialDragGuideView : ITutorialView
    {
        private readonly TutorialPresentationStateStore _store;
        private readonly string _sessionId;
        private readonly string _guideId;

        public TutorialDragGuideView(
            TutorialPresentationStateStore store, string sessionId, string guideId)
        {
            _store = store;
            _sessionId = sessionId;
            _guideId = guideId;
        }

        public void CompleteTutorial()
        {
            _store.RemoveDragGuide(_sessionId, _guideId);
        }
    }
}
