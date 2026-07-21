using TMPro;
using UnityEngine;
using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.UI.KeyControl
{
    public class KeyControlDescription : MonoBehaviour
    {
        public static KeyControlDescription Instance { get; private set; }

        [SerializeField] private TMP_Text keyControlText;
        private string _defaultText = "";
        private string _overrideText;

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
            if (keyControlText != null)
            {
                keyControlText.text = text;
                keyControlText.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            }
        }
    }
}
