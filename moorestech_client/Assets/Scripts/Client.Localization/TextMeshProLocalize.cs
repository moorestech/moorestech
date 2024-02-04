using System;
using TMPro;
using UniRx;
using UnityEngine;

namespace Client.Localization
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextMeshProLocalize : MonoBehaviour
    {
        [SerializeField] private string key;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            _text.text = Localize.Get(key);

            Localize.OnLanguageChanged.Subscribe(_ => GetComponent<TextMeshProUGUI>().text = Localize.Get(key))
                .AddTo(this);
        }

        public void SetKey(string key, params string[] addContents)
        {
            this.key = key;

            var text = string.Empty;
            try
            {
                text = string.Format(Localize.Get(key), addContents);
            }
            catch (FormatException e)
            {
                text = "[Localize] Format Error : " + key;
            }
            catch (Exception e)
            {
                text = $"[Localize] Other Error : {key} : {e.Message}";
            }

            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            _text.text = text;
            _text.ForceMeshUpdate();
        }
    }
}