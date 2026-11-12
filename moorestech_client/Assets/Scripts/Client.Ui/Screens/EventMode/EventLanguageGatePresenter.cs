using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.State.EventMode;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.EventMode
{
    /// <summary>
    /// 言語ゲートの待機中だけ View を層へ載せ、受理で外す。押下と拒否のログは View が持つ
    /// Mounts the view on the layer only while the language gate waits and removes it on acceptance; presses and the rejection log belong to the view
    /// </summary>
    /// <remarks>
    /// DI に載せない: ゲームの DI は言語が選ばれた後に組まれるので、その前に要るこの画面は開始側（Starter の宿主）が作る。ゲート本体 EventLanguageGate と同じ扱い
    /// Not registered in DI: the game DI is built after the language is chosen, so this screen, needed before that, is built by the starter's host, like the EventLanguageGate itself
    /// </remarks>
    public sealed class EventLanguageGatePresenter
    {
        private readonly EventLanguageGate _gate;
        private readonly VisualElement _layer;
        private readonly EventLanguageGateView _view;
        private IDisposable _waitingSubscription;

        public EventLanguageGatePresenter(EventLanguageGate gate, VisualElement layer, StageScaleSource stageScale, IReadOnlyList<LanguageInfo> languages)
        {
            _gate = gate;
            _layer = layer;
            _view = new EventLanguageGateView(gate, stageScale, languages);
        }

        public void Initialize()
        {
            // silent-ok: 待機しないゲートは何も描かない（Web の非表示と同じ）/ 出展モードでない起動は EventModeStartGate が手前で抜けるので、ここへ来るのはテストだけ
            if (!_gate.IsWaitingSelection) return;

            _layer.Add(_view);
            // 受理の通知はその押下の中で同期に来るので、その場で外して二度目の押下を通さない
            // The acceptance notice arrives synchronously inside the press, so remove at once and block a second press
            _waitingSubscription = _gate.OnWaitingChanged.Subscribe(_ => Close());
        }

        private void Close()
        {
            _view.RemoveFromHierarchy();
            _waitingSubscription.Dispose();
        }
    }
}
