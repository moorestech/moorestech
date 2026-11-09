using System.Collections.Generic;
using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Pulse
{
    /// <summary>
    /// 画面ルートで読んだ脈動の周期を、載っている子へ当てる
    /// Applies the pulse period read at the screen root to children while attached
    /// </summary>
    public sealed class TutorialPulseBinding
    {
        private readonly Dictionary<VisualElement, float> _peakScales = new();
        private readonly Dictionary<VisualElement, UiMotionHandle> _handles = new();
        private float _periodMilliseconds;
        private bool _periodResolved;

        public TutorialPulseBinding(VisualElement periodHost)
        {
            periodHost.RegisterCallback<CustomStyleResolvedEvent>(evt => OnPeriodResolved(evt.customStyle));
        }

        public void Attach(VisualElement target, float peakScale)
        {
            // 載っている間だけ流す。載る前・外した後には Play しない（UiMotionPlayer はパネル外で LogError）
            // Pulse only while attached; never Play before attach or after removal (UiMotionPlayer logs off-panel)
            var alreadyAttached = _peakScales.ContainsKey(target);
            _peakScales[target] = peakScale;
            // 2 回目の Attach は振幅だけ差し替えて流し直す（コールバックを二重に張らない）
            // A repeated Attach swaps the amplitude and restarts without registering callbacks twice
            if (alreadyAttached)
            {
                Stop(target);
                if (target.panel != null) Start(target);
                return;
            }
            target.RegisterCallback<AttachToPanelEvent>(_ => Start(target));
            target.RegisterCallback<DetachFromPanelEvent>(_ => Stop(target));
            if (target.panel != null) Start(target);
        }

        public void Detach(VisualElement target)
        {
            // 流しを止めて辞書から外す。Attach のコールバックは要素に残るが、載せ直されても Start は管理外の子を流さない
            // Stop the pulse and drop it from the dictionaries; Attach callbacks stay on the element but Start never pulses an untracked target
            Stop(target);
            _peakScales.Remove(target);
        }

        private void OnPeriodResolved(ICustomStyle customStyle)
        {
            if (!UssCustomStyleReader.TryReadMilliseconds(customStyle, TutorialAttentionPulse.PeriodPropertyName, out var period)) return;
            if (_periodResolved && period == _periodMilliseconds) return;
            _periodMilliseconds = period;
            _periodResolved = true;
            // 周期が変わったら、いま載っている子だけを流し直す
            // When the period changes, restart only the children currently attached
            foreach (var target in _peakScales.Keys)
            {
                if (target.panel == null) continue;
                Stop(target);
                Start(target);
            }
        }

        private void Start(VisualElement target)
        {
            // silent-ok: 周期の解決前は待ち、解決時に OnPeriodResolved が載っている子を流す / L1 TutorialPulseBindingTest が見る
            if (!_peakScales.ContainsKey(target) || !_periodResolved || _handles.ContainsKey(target)) return;
            _handles[target] = TutorialAttentionPulse.Start(target, _peakScales[target], _periodMilliseconds);
        }

        private void Stop(VisualElement target)
        {
            if (!_handles.TryGetValue(target, out var handle)) return;
            handle.Cancel();
            _handles.Remove(target);
        }
    }

}
