// S/Client.Ui/Screens/Skit/Parts/Window/SkitTypewriter.cs
using System;
using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.State.Skit;
using Client.Ui.State.Skit.Logic;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Window
{
    /// <summary>
    /// 本文の何字目まで出したかを持ち、経過時間で 1 字ずつ増やす（Web SkitPresentation の visibleCount と setInterval）。面をまたいで同じ進みを使う
    /// Holds how many characters of the body are out and adds one per interval (Web SkitPresentation visibleCount and setInterval); the same progress spans surfaces
    /// </summary>
    public sealed class SkitTypewriter
    {
        private readonly VisualElement _clockHost;
        private readonly Subject<Unit> _onVisibleCountChanged = new();
        private UiMotionHandle _handle;
        private string _sessionId = string.Empty;
        private string _body = string.Empty;
        private bool _revealsByTypewriter;
        private int _intervalMs;
        private bool _presenting;

        public int VisibleCount { get; private set; }
        public int TotalCount { get; private set; }
        public IObservable<Unit> OnVisibleCountChanged => _onVisibleCountChanged;

        public SkitTypewriter(VisualElement clockHost)
        {
            _clockHost = clockHost;
        }

        // Web の useEffect の依存（session・本文・表示方式・間隔）が変わったときだけ頭から打ち直す
        // Restart from the head only when the Web useEffect dependencies (session, body, mode, interval) change
        public void Follow(SkitViewModel model, bool restoringSnapshot)
        {
            if (!restoringSnapshot && !ChangedFrom(model)) return;
            Remember(model);
            StopTyping();
            TotalCount = SkitTypewriterRules.CountCodePoints(model.Body);

            // 提示なしは 0、復元と即時表示は全文、それ以外は 0 から打つ。間隔 0 以下は即時側で弾くので RevealCountAt は 0 で割らない
            // No presentation means zero, restore and instant mean everything, otherwise type from zero; intervals of 0 or less go the instant way so RevealCountAt never divides by zero
            if (!model.IsPresenting)
            {
                SetVisibleCount(0);
                return;
            }
            if (SkitTypewriterRules.ShouldRevealImmediately(restoringSnapshot, model.RevealsByTypewriter, model.RevealIntervalMs))
            {
                SetVisibleCount(TotalCount);
                return;
            }
            SetVisibleCount(0);
            _handle = UiMotionPlayer.Play(_clockHost, new SkitTypewriterAnimation(this, model.RevealIntervalMs, TotalCount));
        }

        public void RevealAll()
        {
            StopTyping();
            SetVisibleCount(TotalCount);
        }

        internal void SetVisibleCount(int count)
        {
            if (count == VisibleCount) return;
            VisibleCount = count;
            _onVisibleCountChanged.OnNext(Unit.Default);
        }

        private bool ChangedFrom(SkitViewModel model)
        {
            return model.SessionId != _sessionId || model.Body != _body || model.RevealsByTypewriter != _revealsByTypewriter || model.RevealIntervalMs != _intervalMs || model.IsPresenting != _presenting;
        }

        private void Remember(SkitViewModel model)
        {
            _sessionId = model.SessionId;
            _body = model.Body;
            _revealsByTypewriter = model.RevealsByTypewriter;
            _intervalMs = model.RevealIntervalMs;
            _presenting = model.IsPresenting;
        }

        private void StopTyping()
        {
            _handle?.Cancel();
            _handle = null;
        }
    }
}
