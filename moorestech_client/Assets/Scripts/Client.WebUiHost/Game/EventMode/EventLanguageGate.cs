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

        public bool IsWaitingSelection { get; private set; }
        public IObservable<Unit> OnWaitingChanged => _onWaitingChanged;

        // 登録は常に無条件、待つかどうかは初期状態で決める（未登録による固着を避ける）
        // Registration is always unconditional; whether to wait is decided by the initial state to avoid a stuck subscription
        public EventLanguageGate(bool startsWaiting)
        {
            IsWaitingSelection = startsWaiting;
            if (!startsWaiting) _selectionSource.TrySetResult();
        }

        public UniTask WaitForSelectionAsync()
        {
            return _selectionSource.Task;
        }

        // 選択を1回だけ効かせる。二重クリックと再送は「既に選択済み」として区別し、成功と一律に丸めない
        // Only the first selection takes effect; double clicks and resends are distinguished as "already selected" instead of a uniform success
        public EventLanguageSelectionResult TrySelectLanguage(string languageCode)
        {
            if (!IsWaitingSelection) return EventLanguageSelectionResult.AlreadySelected;
            if (!Localize.TrySetLanguage(languageCode)) return EventLanguageSelectionResult.UnknownLanguage;

            IsWaitingSelection = false;
            _onWaitingChanged.OnNext(Unit.Default);
            _selectionSource.TrySetResult();
            return EventLanguageSelectionResult.Applied;
        }
    }

    public enum EventLanguageSelectionResult
    {
        Applied,
        AlreadySelected,
        UnknownLanguage
    }
}
