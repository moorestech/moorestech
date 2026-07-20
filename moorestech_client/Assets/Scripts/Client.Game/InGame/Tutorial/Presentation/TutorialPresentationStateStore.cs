using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;

namespace Client.Game.InGame.Tutorial
{
    public class TutorialPresentationStateStore
    {
        private readonly Subject<TutorialPresentationData> _onChanged = new();
        private TutorialPresentationData _current = CreateIdle();

        public static readonly TutorialPresentationStateStore Instance = new();

        public IObservable<TutorialPresentationData> ObserveChanged()
        {
            return _onChanged;
        }

        public TutorialPresentationData GetCurrent()
        {
            return _current;
        }

        // challenge適用ごとに新sessionを発行し、前challengeのDOM宣言を切り離す
        // Issue a new session per challenge application to detach prior DOM declarations
        public void BeginSession(Guid challengeId)
        {
            _current = new TutorialPresentationData
            {
                TutorialSessionId = Guid.NewGuid().ToString(),
                Revision = 0,
                ChallengeId = challengeId.ToString(),
                Highlights = Array.Empty<TutorialHighlightData>(),
            };
            Publish();
        }

        // highlightごとのviewを返し、既存challenge lifecycleから同じ宣言を除去できるようにする
        // Return a per-highlight view so the existing challenge lifecycle can remove that declaration
        public ITutorialView AddHighlight(string anchorId, string kind, string message)
        {
            var highlight = new TutorialHighlightData
            {
                HighlightId = Guid.NewGuid().ToString(),
                AnchorId = anchorId,
                Kind = kind,
                Message = message,
                PaddingPx = 8,
                BlocksPointerInput = false,
            };
            var highlights = new List<TutorialHighlightData>(_current.Highlights) { highlight };
            SetHighlights(highlights.ToArray());
            return new TutorialPresentationView(this, _current.TutorialSessionId, highlight.HighlightId);
        }

        // 過去challengeの完了通知は現在sessionへ波及させない
        // Prevent completion of an older challenge from mutating the current session
        public void EndSession(Guid challengeId)
        {
            if (_current.ChallengeId != challengeId.ToString()) return;
            if (_current.Highlights.Length == 0) return;
            SetHighlights(Array.Empty<TutorialHighlightData>());
        }

        public bool Matches(string sessionId, int revision)
        {
            return sessionId == _current.TutorialSessionId && revision == _current.Revision;
        }

        public bool IsCurrentChallenge(Guid challengeId)
        {
            return _current.ChallengeId == challengeId.ToString();
        }

        public void RemoveHighlight(string sessionId, string highlightId)
        {
            if (sessionId != _current.TutorialSessionId) return;
            var highlights = _current.Highlights.Where(value => value.HighlightId != highlightId).ToArray();
            if (highlights.Length == _current.Highlights.Length) return;
            SetHighlights(highlights);
        }

        private void SetHighlights(TutorialHighlightData[] highlights)
        {
            _current = new TutorialPresentationData
            {
                TutorialSessionId = _current.TutorialSessionId,
                Revision = _current.Revision + 1,
                ChallengeId = _current.ChallengeId,
                Highlights = highlights,
            };
            Publish();
        }

        private void Publish()
        {
            _onChanged.OnNext(_current);
        }

        private static TutorialPresentationData CreateIdle()
        {
            return new TutorialPresentationData
            {
                TutorialSessionId = "", Revision = 0, ChallengeId = "",
                Highlights = Array.Empty<TutorialHighlightData>(),
            };
        }
    }

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
}
