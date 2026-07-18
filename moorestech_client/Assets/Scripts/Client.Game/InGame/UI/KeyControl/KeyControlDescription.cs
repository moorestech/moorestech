using TMPro;
using UnityEngine;
using System;
using Client.Game.InGame.UI.UIState;
using UniRx;

namespace Client.Game.InGame.UI.KeyControl
{
    public class KeyControlDescription : MonoBehaviour
    {
        public static KeyControlDescription Instance { get; private set; }
        
        [SerializeField] private TMP_Text keyControlText;
        private readonly ReactiveProperty<string> _text = new("");
        private string _defaultText = "";
        private string _overrideText;

        public IObservable<string> OnTextChanged => _text;
        public string GetText() => _text.Value;
        
        private void Awake()
        {
            Instance = this;
        }
        
        public void SetText(string text)
        {
            _defaultText = text;
            RefreshText();
        }

        public void SetOverrideText(string text)
        {
            _overrideText = text;
            RefreshText();
        }

        public void ClearOverrideText()
        {
            _overrideText = null;
            RefreshText();
        }

        private void RefreshText()
        {
            var text = _overrideText ?? _defaultText;
            _text.Value = text;
            if (keyControlText != null)
            {
                keyControlText.text = text;
                keyControlText.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            }
        }
    }
}
