using System;
using Client.Game.InGame.UI.UIState;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Blueprint
{
    /// <summary>
    ///     BP名入力ダイアログ（確定/キャンセルをUniRx通知）
    ///     Blueprint name input dialog; confirm and cancel are published via UniRx
    /// </summary>
    public class BlueprintNameInputView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private readonly Subject<string> _onConfirm = new();
        private readonly Subject<Unit> _onCancel = new();

        // 開閉状態（webブリッジの購読用。ビューが状態権威）
        // Open/close state (subscribed by the web bridge; this view owns the state)
        public bool IsOpen { get; private set; }
        public IObservable<bool> OnOpenChanged => _onOpenChanged;
        private readonly Subject<bool> _onOpenChanged = new();

        public IObservable<string> OnConfirm => _onConfirm;
        public IObservable<Unit> OnCancel => _onCancel;

        private void Awake()
        {
            // 空白のみの名前は確定させない
            // Reject whitespace-only names on confirm
            confirmButton.onClick.AddListener(() =>
            {
                if (string.IsNullOrWhiteSpace(nameInputField.text)) return;
                _onConfirm.OnNext(nameInputField.text.Trim());
                Close();
            });
            cancelButton.onClick.AddListener(() =>
            {
                _onCancel.OnNext(Unit.Default);
                Close();
            });
        }

        public void Open()
        {
            nameInputField.text = "";
            IsOpen = true;

            // webモード中は置換済みビューとしてuGUI表示を抑止する（状態と通知は維持）
            // In web mode suppress the uGUI visual as a replaced view (state and notifications stay live)
            var visible = !WebUiScreenGate.IsWebUiMode;
            gameObject.SetActive(visible);
            if (visible) nameInputField.ActivateInputField();

            _onOpenChanged.OnNext(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (!IsOpen) return;
            IsOpen = false;
            _onOpenChanged.OnNext(false);
        }

        // webモーダルからの確定（uGUIボタンと同一の空白検証・Trim・通知経路）
        // Confirm from the web modal (same whitespace validation, trim, and notification path as the uGUI button)
        public void SetConfirmFromWeb(string name)
        {
            if (!IsOpen) return;
            if (string.IsNullOrWhiteSpace(name)) return;
            _onConfirm.OnNext(name.Trim());
            Close();
        }

        // webモーダルからのキャンセル
        // Cancel from the web modal
        public void SetCancelFromWeb()
        {
            if (!IsOpen) return;
            _onCancel.OnNext(Unit.Default);
            Close();
        }
    }
}
