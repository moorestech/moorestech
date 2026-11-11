using System;
using System.Collections.Generic;
using Client.Skit.UI;
using UnityEngine;

namespace Client.Ui.State.Skit
{
    /// <summary>
    /// スキットの提示 1 回を「出す面・押せる操作・打ち出し方」に畳んだ値（Web SkitPresentation の分岐の写し。A-81）
    /// One skit presentation folded into the surface, the pressable operations and the reveal style (a copy of the Web SkitPresentation branches; A-81)
    /// </summary>
    public sealed class SkitViewModel
    {
        public static readonly SkitViewModel Empty = new(SkitSurface.None, false, string.Empty, 0, string.Empty, string.Empty, Array.Empty<SkitChoiceItem>(), false, false, false, false, false, false, false, 0);

        public SkitSurface Surface { get; }
        public bool IsPresenting { get; }
        public string SessionId { get; }
        public int SceneRevision { get; }
        public string SpeakerName { get; }
        public string Body { get; }
        public IReadOnlyList<SkitChoiceItem> Choices { get; }
        public bool CanAdvance { get; }
        public bool CanSetAuto { get; }
        public bool CanSkip { get; }
        public bool CanSetUiHidden { get; }
        public bool AutoEnabled { get; }
        public bool TransitionVisible { get; }
        public bool RevealsByTypewriter { get; }
        public int RevealIntervalMs { get; }

        private SkitViewModel(SkitSurface surface, bool isPresenting, string sessionId, int sceneRevision, string speakerName, string body, IReadOnlyList<SkitChoiceItem> choices, bool canAdvance, bool canSetAuto, bool canSkip, bool canSetUiHidden, bool autoEnabled, bool transitionVisible, bool revealsByTypewriter, int revealIntervalMs)
        {
            Surface = surface;
            IsPresenting = isPresenting;
            SessionId = sessionId;
            SceneRevision = sceneRevision;
            SpeakerName = speakerName;
            Body = body;
            Choices = choices;
            CanAdvance = canAdvance;
            CanSetAuto = canSetAuto;
            CanSkip = canSkip;
            CanSetUiHidden = canSetUiHidden;
            AutoEnabled = autoEnabled;
            TransitionVisible = transitionVisible;
            RevealsByTypewriter = revealsByTypewriter;
            RevealIntervalMs = revealIntervalMs;
        }

        public static SkitViewModel From(SkitPresentationData data)
        {
            // 出す面を Web と同じ順の分岐で決め、選択肢は窓が出ていて select が許可されたときだけ渡す
            // Decide the surface with the Web's branch order, and pass choices only when the window shows and select is allowed
            var state = data.PresentationState;
            var intents = data.AllowedIntents;
            var presenting = state.Mode == SkitPresentationData.BlockingMode || state.Mode == SkitPresentationData.BackgroundMode;
            var surface = SurfaceOf();
            var choices = surface == SkitSurface.Window && Allows(SkitPresentationData.SelectIntent) ? ToItems(state.Choices) : Array.Empty<SkitChoiceItem>();

            // intent の綴りと許可の判定は持ち主の SkitPresentationData を読む。スキップ中のスキップは押せない（Web の disabled={!allowed || skipActive}）
            // Intent spellings and the allow check come from the owner SkitPresentationData; skip cannot be pressed while skipping (Web disabled={!allowed || skipActive})
            return new SkitViewModel(surface, presenting, data.SessionId, data.SceneRevision, state.SpeakerName, state.Body, choices,
                Allows(SkitPresentationData.AdvanceIntent), Allows(SkitPresentationData.SetAutoIntent), Allows(SkitPresentationData.SkipIntent) && !state.SkipActive, Allows(SkitPresentationData.SetUiHiddenIntent),
                state.AutoEnabled, presenting && state.TransitionVisible, state.TextReveal.Mode == SkitPresentationData.TypewriterRevealMode, state.TextReveal.IntervalMs);

            #region Internal

            SkitSurface SurfaceOf()
            {
                // 未知のモードは何も出さず、名前を開発者ログへ出す
                // An unknown mode shows nothing and its name goes to the developer log
                if (state.Mode == SkitPresentationData.NoneMode) return SkitSurface.None;
                if (!presenting)
                {
                    Debug.LogError($"[SkitViewModel] unknown presentation mode '{state.Mode}', nothing is shown");
                    return SkitSurface.None;
                }

                var blocking = state.Mode == SkitPresentationData.BlockingMode;
                if (state.UiHidden) return blocking ? SkitSurface.RestoreOnly : SkitSurface.None;
                if (!state.TextAreaVisible) return blocking ? SkitSurface.ToolbarOnly : SkitSurface.None;
                if (blocking) return SkitSurface.Window;
                return string.IsNullOrEmpty(state.Body) ? SkitSurface.None : SkitSurface.BackgroundLine;
            }

            bool Allows(string intent)
            {
                return SkitPresentationData.IsIntentAllowed(intents, intent);
            }

            static IReadOnlyList<SkitChoiceItem> ToItems(SkitChoice[] source)
            {
                var items = new SkitChoiceItem[source.Length];
                for (var i = 0; i < source.Length; i++) items[i] = new SkitChoiceItem(source[i].ChoiceId, source[i].Label);
                return items;
            }

            #endregion
        }
    }
}
