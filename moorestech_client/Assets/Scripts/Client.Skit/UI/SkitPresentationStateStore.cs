using System;
using UniRx;

namespace Client.Skit.UI
{
    public class SkitPresentationStateStore
    {
        private readonly Subject<SkitPresentationData> _onChanged = new();
        public static SkitPresentationStateStore Instance { get; } = new();
        public IObservable<SkitPresentationData> OnChanged => _onChanged;
        public SkitPresentationData Current { get; private set; } = SkitPresentationData.None("", 0);

        public void BeginBackground()
        {
            Current = SkitPresentationData.Background(Guid.NewGuid().ToString(), 0, "", "");
            _onChanged.OnNext(Current);
        }

        public void SetBackgroundText(string speakerName, string body)
        {
            Current = SkitPresentationData.Background(Current.SessionId, Current.SceneRevision + 1, speakerName, body);
            _onChanged.OnNext(Current);
        }

        public void End()
        {
            Current = SkitPresentationData.None(Current.SessionId, Current.SceneRevision + 1);
            _onChanged.OnNext(Current);
        }
    }

    public class SkitPresentationData
    {
        public string SessionId;
        public int SceneRevision;
        public SkitPresentationState PresentationState;
        public string[] AllowedIntents;

        public static SkitPresentationData None(string sessionId, int revision)
        {
            return Create(sessionId, revision, "none", "", "", false, "instant", 0);
        }

        public static SkitPresentationData Background(string sessionId, int revision, string speaker, string body)
        {
            return Create(sessionId, revision, "background", speaker, body, true, "typewriter", 24);
        }

        private static SkitPresentationData Create(string sessionId, int revision, string mode, string speaker,
            string body, bool visible, string revealMode, int intervalMs)
        {
            return new SkitPresentationData
            {
                SessionId = sessionId,
                SceneRevision = revision,
                AllowedIntents = Array.Empty<string>(),
                PresentationState = new SkitPresentationState
                {
                    Mode = mode, SpeakerName = speaker, Body = body, Choices = Array.Empty<SkitChoice>(),
                    TextAreaVisible = visible, TextReveal = new SkitTextReveal { Mode = revealMode, IntervalMs = intervalMs }
                }
            };
        }
    }

    public class SkitPresentationState
    {
        public string Mode;
        public string SpeakerName;
        public string Body;
        public SkitChoice[] Choices;
        public bool TextAreaVisible;
        public bool TransitionVisible;
        public bool AutoEnabled;
        public bool SkipActive;
        public bool UiHidden;
        public SkitTextReveal TextReveal;
    }

    public class SkitChoice { public string ChoiceId; public string Label; }
    public class SkitTextReveal { public string Mode; public int IntervalMs; }
}
