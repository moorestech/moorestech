using System;
using UniRx;

namespace Client.Game.InGame.UI.Blueprint
{
    /// <summary>
    ///     BP名入力の開閉と確定/キャンセルの通知。入力欄そのものは Web のモーダルが担う
    ///     Open/close state and confirm/cancel notifications of the blueprint-name input; the web modal owns the field
    /// </summary>
    public class BlueprintNameInputState
    {
        public bool IsOpen { get; private set; }
        public IObservable<bool> OnOpenChanged => _onOpenChanged;
        public IObservable<string> OnConfirm => _onConfirm;
        public IObservable<Unit> OnCancel => _onCancel;

        private readonly Subject<bool> _onOpenChanged = new();
        private readonly Subject<string> _onConfirm = new();
        private readonly Subject<Unit> _onCancel = new();

        public void Open()
        {
            IsOpen = true;
            _onOpenChanged.OnNext(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _onOpenChanged.OnNext(false);
        }

        // 空白のみの名前は確定させない
        // Reject whitespace-only names on confirm
        public void Confirm(string name)
        {
            if (!IsOpen) return;
            if (string.IsNullOrWhiteSpace(name)) return;
            _onConfirm.OnNext(name.Trim());
            Close();
        }

        public void Cancel()
        {
            if (!IsOpen) return;
            _onCancel.OnNext(Unit.Default);
            Close();
        }
    }
}
