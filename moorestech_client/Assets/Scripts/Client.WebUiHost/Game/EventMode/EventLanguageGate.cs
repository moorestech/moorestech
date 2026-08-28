using System;
using Client.Localization;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.EventMode
{
    /// <summary>
    /// 言語が選ばれるまでゲーム開始を止める出展モードの開始ゲート。
    /// The event-mode start gate that holds the game start until a language is chosen.
    /// </summary>
    public class EventLanguageGate
    {
        private readonly UniTaskCompletionSource _selectionSource = new();
        private readonly Subject<Unit> _onWaitingChanged = new();

        public bool IsWaitingSelection { get; private set; } = true;
        public IObservable<Unit> OnWaitingChanged => _onWaitingChanged;

        public UniTask WaitForSelectionAsync()
        {
            return _selectionSource.Task;
        }

        // 選択を1回だけ効かせる。二重クリックと再送は成功として捨て、ゲートを二度開けない
        // Only the first selection takes effect; double clicks and resends succeed as no-ops
        public bool TrySelectLanguage(string languageCode)
        {
            if (!IsWaitingSelection) return true;
            if (!Localize.TrySetLanguage(languageCode)) return false;

            IsWaitingSelection = false;
            _onWaitingChanged.OnNext(Unit.Default);
            _selectionSource.TrySetResult();
            return true;
        }
    }
}
