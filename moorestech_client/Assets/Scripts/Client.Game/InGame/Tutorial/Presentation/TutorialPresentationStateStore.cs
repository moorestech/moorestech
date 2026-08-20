using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;

namespace Client.Game.InGame.Tutorial
{
    public class TutorialPresentationStateStore
    {
        private readonly Subject<TutorialPresentationData> _onChanged = new();
        private readonly List<TutorialSessionData> _sessions = new();
        private TutorialPresentationData _current = CreateIdle();
        private int _revision;

        // 追加先は直近にBeginSessionしたchallenge。TutorialManagerが1challengeずつ同期適用する
        // Elements attach to the most recently begun challenge; TutorialManager applies one challenge synchronously
        private string _applyTargetChallengeId = "";

        public static readonly TutorialPresentationStateStore Instance = new();

        public IObservable<TutorialPresentationData> ObserveChanged()
        {
            return _onChanged;
        }

        public TutorialPresentationData GetCurrent()
        {
            return _current;
        }

        // challengeごとに独立したsessionを持ち、複数challengeが同時currentでも先行提示を消さない
        // Keep one session per challenge so simultaneous current challenges never erase each other's presentation
        public void BeginSession(Guid challengeId)
        {
            var challengeKey = challengeId.ToString();
            _sessions.RemoveAll(session => session.ChallengeId == challengeKey);
            _sessions.Add(new TutorialSessionData
            {
                TutorialSessionId = Guid.NewGuid().ToString(),
                ChallengeId = challengeKey,
                Elements = Array.Empty<TutorialOverlayElementData>(),
            });
            _applyTargetChallengeId = challengeKey;
            Publish();
        }

        // outline専用に限定(廃止kind再流入防止)。ラベル有無判定はここへ集約
        // Outline-only, to prevent removed kinds from returning; label presence decision lives here
        public ITutorialView AddOutlineHighlight(string anchorId, string labelText, Guid tutorialGuid)
        {
            var labelTutorialGuid = string.IsNullOrEmpty(labelText) ? null : tutorialGuid.ToString();
            return AddElement(new TutorialOutlineElementData
            {
                ElementId = Guid.NewGuid().ToString(),
                AnchorId = anchorId,
                PaddingPx = 8,
                BlocksPointerInput = false,
                LabelTutorialGuid = labelTutorialGuid,
            });
        }

        // D&D操作の説明矢印。from→toのanchor間ループはWeb側が描く
        // D&D guide arrow; the web side draws the looping motion between the anchors
        public ITutorialView AddDragGuide(string fromAnchorId, string toAnchorId)
        {
            return AddElement(new TutorialDragGuideElementData
            {
                ElementId = Guid.NewGuid().ToString(),
                FromAnchorId = fromAnchorId,
                ToAnchorId = toAnchorId,
            });
        }

        // キー操作ヒント。表示可否はWeb側判定
        // Key-control hint; visibility is decided web-side
        public ITutorialView AddKeyControlHint(string tutorialGuid, string keyName, string uiState)
        {
            return AddElement(new TutorialKeyControlElementData
            {
                ElementId = Guid.NewGuid().ToString(),
                TutorialGuid = tutorialGuid,
                KeyName = keyName,
                UiState = uiState,
            });
        }

        // 完了したchallengeのsessionだけを畳み、他challengeの提示は残す
        // Drop only the completed challenge's session and leave the other challenges' presentation intact
        public void EndSession(Guid challengeId)
        {
            var challengeKey = challengeId.ToString();
            if (_sessions.RemoveAll(session => session.ChallengeId == challengeKey) == 0) return;
            if (_applyTargetChallengeId == challengeKey) _applyTargetChallengeId = "";
            Publish();
        }

        public bool HasSessionId(string sessionId)
        {
            return _sessions.Any(session => session.TutorialSessionId == sessionId);
        }

        public void RemoveElement(string sessionId, string elementId)
        {
            var session = _sessions.Find(value => value.TutorialSessionId == sessionId);
            if (session == null) return;
            var elements = session.Elements.Where(value => value.ElementId != elementId).ToArray();
            if (elements.Length == session.Elements.Length) return;
            ReplaceSession(session, elements);
        }

        private ITutorialView AddElement(TutorialOverlayElementData element)
        {
            var session = GetOrCreateApplyTarget();
            var elements = new List<TutorialOverlayElementData>(session.Elements) { element };
            ReplaceSession(session, elements.ToArray());
            return new TutorialOverlayElementView(this, session.TutorialSessionId, element.ElementId);
        }

        // WebUIモード外ではBeginSessionを経ず追加されるため、challenge無しsessionで受ける
        // Outside web UI mode elements arrive without BeginSession, so accept them into a challenge-less session
        private TutorialSessionData GetOrCreateApplyTarget()
        {
            var session = _sessions.Find(value => value.ChallengeId == _applyTargetChallengeId);
            if (session != null) return session;
            session = new TutorialSessionData
            {
                TutorialSessionId = Guid.NewGuid().ToString(),
                ChallengeId = _applyTargetChallengeId,
                Elements = Array.Empty<TutorialOverlayElementData>(),
            };
            _sessions.Add(session);
            return session;
        }

        // 公開済みsnapshotを書き換えないよう、session単位でも差し替える
        // Replace the session object as well so an already published snapshot is never mutated
        private void ReplaceSession(TutorialSessionData session, TutorialOverlayElementData[] elements)
        {
            _sessions[_sessions.IndexOf(session)] = new TutorialSessionData
            {
                TutorialSessionId = session.TutorialSessionId,
                ChallengeId = session.ChallengeId,
                Elements = elements,
            };
            Publish();
        }

        private void Publish()
        {
            _revision++;
            _current = new TutorialPresentationData { Revision = _revision, Sessions = _sessions.ToArray() };
            _onChanged.OnNext(_current);
        }

        private static TutorialPresentationData CreateIdle()
        {
            return new TutorialPresentationData { Revision = 0, Sessions = Array.Empty<TutorialSessionData>() };
        }
    }
}
