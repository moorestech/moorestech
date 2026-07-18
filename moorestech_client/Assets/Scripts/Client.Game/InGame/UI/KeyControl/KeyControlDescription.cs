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

        public IObservable<string> OnTextChanged => _text;
        public string GetText() => _text.Value;
        
        private void Awake()
        {
            Instance = this;
        }
        
        public void SetText(string text)
        {
            _text.Value = text;
            if (keyControlText != null)
            {
                keyControlText.text = text;
                keyControlText.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            }
        }
    }
}
