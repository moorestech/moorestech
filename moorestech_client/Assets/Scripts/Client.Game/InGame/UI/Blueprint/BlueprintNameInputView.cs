using System;
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
            gameObject.SetActive(true);
            nameInputField.ActivateInputField();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
