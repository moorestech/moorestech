using System;

namespace Client.Skit.UI
{
    public class SkitPresentationData
    {
        // blocking スキット（会話の演出が画面を持つ）のモード名。判定する側もこの定数を読む
        // Mode name of a blocking skit (the dialogue owns the screen); consumers compare against this constant too
        public const string BlockingMode = "blocking";
        public const string NoneMode = "none";
        public const string BackgroundMode = "background";

        // 許可 intent と表示方式の綴り。Web の wire と同じで、store・UITK・Playtest が読む
        // Spellings of the allowed intents and reveal modes; same as the Web wire, read by the store, UITK and Playtest
        public const string AdvanceIntent = "advance";
        public const string SelectIntent = "select";
        public const string SetAutoIntent = "set-auto";
        public const string SkipIntent = "skip";
        public const string SetUiHiddenIntent = "set-ui-hidden";
        public const string TypewriterRevealMode = "typewriter";
        public const string InstantRevealMode = "instant";

        // 背景スキットの 1 字あたりの打ち出し間隔（ms）。blocking の間隔は store の TypewriterIntervalMs が持つ
        // Per-character typing interval (ms) of a background skit; the blocking interval is the store's TypewriterIntervalMs
        private const int BackgroundTypewriterIntervalMs = 24;

        // 許可 intent に含まれるか。store の検証・UITK の ViewModel・Playtest が同じ規則を読む
        // Whether the intent is in the allowed set; the store validation, the UITK view model and Playtest share this rule
        public static bool IsIntentAllowed(string[] allowedIntents, string intent)
        {
            return 0 <= Array.IndexOf(allowedIntents, intent);
        }

        public string SessionId;
        public int SceneRevision;
        public SkitPresentationState PresentationState;
        public string[] AllowedIntents;

        public static SkitPresentationData CreateNone(string sessionId, int revision)
        {
            return CreateBlocking(sessionId, revision, "", "", Array.Empty<SkitChoice>(), false, false,
                false, false, false, InstantRevealMode, 0, Array.Empty<string>(), NoneMode);
        }

        public static SkitPresentationData CreateBackground(string sessionId, int revision, string speaker, string body)
        {
            return CreateBlocking(sessionId, revision, speaker, body, Array.Empty<SkitChoice>(), true, false,
                false, false, false, TypewriterRevealMode, BackgroundTypewriterIntervalMs, Array.Empty<string>(), BackgroundMode);
        }

        public static SkitPresentationData CreateBlocking(string sessionId, int revision, string speaker, string body,
            SkitChoice[] choices, bool textAreaVisible, bool transitionVisible, bool autoEnabled, bool skipActive,
            bool uiHidden, string revealMode, int intervalMs, string[] allowedIntents)
        {
            return CreateBlocking(sessionId, revision, speaker, body, choices, textAreaVisible, transitionVisible,
                autoEnabled, skipActive, uiHidden, revealMode, intervalMs, allowedIntents, BlockingMode);
        }

        public SkitPresentationData CopyWith(int revision, string[] allowedIntents)
        {
            return CreateBlocking(SessionId, revision, PresentationState.SpeakerName, PresentationState.Body,
                PresentationState.Choices, PresentationState.TextAreaVisible, PresentationState.TransitionVisible,
                PresentationState.AutoEnabled, PresentationState.SkipActive, PresentationState.UiHidden,
                PresentationState.TextReveal.Mode, PresentationState.TextReveal.IntervalMs, allowedIntents,
                PresentationState.Mode);
        }

        public SkitPresentationData CopyWithControls(int revision, bool autoEnabled, bool skipActive, bool uiHidden)
        {
            return CreateBlocking(SessionId, revision, PresentationState.SpeakerName, PresentationState.Body,
                PresentationState.Choices, PresentationState.TextAreaVisible, PresentationState.TransitionVisible,
                autoEnabled, skipActive, uiHidden, PresentationState.TextReveal.Mode,
                PresentationState.TextReveal.IntervalMs, AllowedIntents, PresentationState.Mode);
        }

        public SkitPresentationData CopyWithChoices(int revision, SkitChoice[] choices, string[] allowedIntents)
        {
            return CreateBlocking(SessionId, revision, PresentationState.SpeakerName, PresentationState.Body,
                choices, PresentationState.TextAreaVisible, PresentationState.TransitionVisible,
                PresentationState.AutoEnabled, PresentationState.SkipActive, PresentationState.UiHidden,
                PresentationState.TextReveal.Mode, PresentationState.TextReveal.IntervalMs, allowedIntents,
                PresentationState.Mode);
        }

        public SkitPresentationData CopyWithScreen(int revision, bool textAreaVisible, bool transitionVisible)
        {
            return CreateBlocking(SessionId, revision, PresentationState.SpeakerName, PresentationState.Body,
                PresentationState.Choices, textAreaVisible, transitionVisible, PresentationState.AutoEnabled,
                PresentationState.SkipActive, PresentationState.UiHidden, PresentationState.TextReveal.Mode,
                PresentationState.TextReveal.IntervalMs, AllowedIntents, PresentationState.Mode);
        }

        private static SkitPresentationData CreateBlocking(string sessionId, int revision, string speaker, string body,
            SkitChoice[] choices, bool textAreaVisible, bool transitionVisible, bool autoEnabled, bool skipActive,
            bool uiHidden, string revealMode, int intervalMs, string[] allowedIntents, string mode)
        {
            return new SkitPresentationData
            {
                SessionId = sessionId,
                SceneRevision = revision,
                AllowedIntents = allowedIntents,
                PresentationState = new SkitPresentationState
                {
                    Mode = mode,
                    SpeakerName = speaker,
                    Body = body,
                    Choices = choices,
                    TextAreaVisible = textAreaVisible,
                    TransitionVisible = transitionVisible,
                    AutoEnabled = autoEnabled,
                    SkipActive = skipActive,
                    UiHidden = uiHidden,
                    TextReveal = new SkitTextReveal { Mode = revealMode, IntervalMs = intervalMs },
                },
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

    public class SkitChoice
    {
        public string ChoiceId;
        public string LabelKey;
        public string Label;
    }

    public class SkitTextReveal
    {
        public string Mode;
        public int IntervalMs;
    }

    public readonly struct SkitIntentResult
    {
        public readonly bool Ok;
        public readonly string Error;

        private SkitIntentResult(bool ok, string error)
        {
            Ok = ok;
            Error = error;
        }

        public static SkitIntentResult Success()
        {
            return new SkitIntentResult(true, null);
        }

        public static SkitIntentResult Fail(string error)
        {
            return new SkitIntentResult(false, error);
        }
    }

    public static class SkitChoiceJumpResolver
    {
        public static int ResolveSelectedIndex(string choiceId, SkitChoice[] choices)
        {
            for (var index = 0; index < choices.Length; index++)
            {
                if (choices[index].ChoiceId == choiceId) return index;
            }
            return -1;
        }
    }
}
