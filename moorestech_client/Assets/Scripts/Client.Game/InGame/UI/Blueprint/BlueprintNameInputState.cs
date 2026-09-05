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
        private bool _isOpen;
        public IObservable<bool> OnOpenChanged => _onOpenChanged;
        public IObservable<string> OnConfirm => _onConfirm;
        public IObservable<Unit> OnCancel => _onCancel;

        private readonly Subject<bool> _onOpenChanged = new();
        private readonly Subject<string> _onConfirm = new();
        private readonly Subject<Unit> _onCancel = new();

        public void Open()
        {
            _isOpen = true;
            _onOpenChanged.OnNext(true);
        }

        internal void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _onOpenChanged.OnNext(false);
        }

        // 空白のみの名前は確定させない
        // Reject whitespace-only names on confirm
        public void Confirm(string name)
        {
            if (!_isOpen) return;
            if (string.IsNullOrWhiteSpace(name)) return;
            _onConfirm.OnNext(name.Trim());
            Close();
        }

        public void Cancel()
        {
            if (!_isOpen) return;
            _onCancel.OnNext(Unit.Default);
            Close();
        }
    }
}
