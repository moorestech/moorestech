using System;
using Client.Skit.UI;
using Client.Ui.State.Skit;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// スキットの L1 の提示。Web の mock（話者 Moore・session blocking-1・Route A/B）と同じ値を本物の生成から作る
    /// Skit L1 snapshots built through the real factories with the Web mock values (speaker Moore, session blocking-1, Route A/B)
    /// </summary>
    public static class SkitTestData
    {
        public const string SessionId = "blocking-1";
        public static readonly string[] TextIntents = { "advance", "set-auto", "skip", "set-ui-hidden" };
        public static readonly string[] ChoiceIntents = { "select", "set-auto", "skip", "set-ui-hidden" };

        public static SkitViewModel Text(string sessionId, int revision, string body, string[] intents)
        {
            return SkitViewModel.From(SkitPresentationData.CreateBlocking(sessionId, revision, "Moore", body, Array.Empty<SkitChoice>(), true, false, false, false, false, "typewriter", 50, intents));
        }

        public static SkitViewModel Choices(int revision, string[] intents)
        {
            var choices = new[] { new SkitChoice { ChoiceId = "route-a", Label = "Route A" }, new SkitChoice { ChoiceId = "route-b", Label = "Route B" } };
            return SkitViewModel.From(SkitPresentationData.CreateBlocking(SessionId, revision, "Moore", "Blocking message", choices, true, false, false, false, false, "instant", 0, intents));
        }

        public static SkitViewModel Controls(int revision, bool textAreaVisible, bool transitionVisible, bool autoEnabled, bool skipActive, bool uiHidden, string[] intents)
        {
            return SkitViewModel.From(SkitPresentationData.CreateBlocking(SessionId, revision, "Moore", "Blocking message", Array.Empty<SkitChoice>(), textAreaVisible, transitionVisible, autoEnabled, skipActive, uiHidden, "instant", 0, intents));
        }

        public static SkitViewModel Background(int revision, string speaker, string body)
        {
            return SkitViewModel.From(SkitPresentationData.CreateBackground("background-1", revision, speaker, body));
        }

        public static SkitViewModel None(int revision)
        {
            return SkitViewModel.From(SkitPresentationData.CreateNone(SessionId, revision));
        }
    }
}
