namespace Client.Game.InGame.Tutorial
{
    // highlight/dragGuideを問わずsessionId＋elementIdで1本の撤去経路に畳む
    // Both highlights and drag guides are removed through one path keyed by sessionId + elementId
    public class TutorialOverlayElementView : ITutorialView
    {
        private readonly TutorialPresentationStateStore _store;
        private readonly string _sessionId;
        private readonly string _elementId;

        public TutorialOverlayElementView(
            TutorialPresentationStateStore store, string sessionId, string elementId)
        {
            _store = store;
            _sessionId = sessionId;
            _elementId = elementId;
        }

        public void CompleteTutorial()
        {
            _store.RemoveElement(_sessionId, _elementId);
        }
    }
}
