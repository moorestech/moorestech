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
                DragGuides = Array.Empty<TutorialDragGuideData>(),
            };
            Publish();
        }

        // outline用途だけを公開し、廃止済みkindの再流入を防ぐ
        // Expose only the outline use case to prevent removed kinds from returning
        public ITutorialView AddOutlineHighlight(string anchorId)
        {
            var highlight = new TutorialHighlightData
            {
                HighlightId = Guid.NewGuid().ToString(),
                AnchorId = anchorId,
                Kind = "outline",
                PaddingPx = 8,
                BlocksPointerInput = false,
            };
            var highlights = new List<TutorialHighlightData>(_current.Highlights) { highlight };
            SetState(highlights.ToArray(), _current.DragGuides);
            return new TutorialPresentationView(this, _current.TutorialSessionId, highlight.HighlightId);
        }

        // D&D操作の説明矢印。from→toのanchor間ループはWeb側が描く
        // D&D guide arrow; the web side draws the looping motion between the anchors
        public ITutorialView AddDragGuide(string fromAnchorId, string toAnchorId)
        {
            var guide = new TutorialDragGuideData
            {
                GuideId = Guid.NewGuid().ToString(),
                FromAnchorId = fromAnchorId,
                ToAnchorId = toAnchorId,
            };
            var guides = new List<TutorialDragGuideData>(_current.DragGuides) { guide };
            SetState(_current.Highlights, guides.ToArray());
            return new TutorialDragGuideView(this, _current.TutorialSessionId, guide.GuideId);
        }

        // 過去challengeの完了通知は現在sessionへ波及させない
        // Prevent completion of an older challenge from mutating the current session
        public void EndSession(Guid challengeId)
        {
            if (_current.ChallengeId != challengeId.ToString()) return;
            if (_current.Highlights.Length == 0 && _current.DragGuides.Length == 0) return;
            SetState(Array.Empty<TutorialHighlightData>(), Array.Empty<TutorialDragGuideData>());
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
            SetState(highlights, _current.DragGuides);
        }

        public void RemoveDragGuide(string sessionId, string guideId)
        {
            if (sessionId != _current.TutorialSessionId) return;
            var guides = _current.DragGuides.Where(value => value.GuideId != guideId).ToArray();
            if (guides.Length == _current.DragGuides.Length) return;
            SetState(_current.Highlights, guides);
        }

        private void SetState(TutorialHighlightData[] highlights, TutorialDragGuideData[] dragGuides)
        {
            _current = new TutorialPresentationData
            {
                TutorialSessionId = _current.TutorialSessionId,
                Revision = _current.Revision + 1,
                ChallengeId = _current.ChallengeId,
                Highlights = highlights,
                DragGuides = dragGuides,
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
                DragGuides = Array.Empty<TutorialDragGuideData>(),
            };
        }
    }
}
