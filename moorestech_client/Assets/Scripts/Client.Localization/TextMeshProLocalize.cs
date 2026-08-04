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
            _text.text = Localize.GetLegacy(key);
            
            Localize.OnLanguageChanged.Subscribe(_ => GetComponent<TextMeshProUGUI>().text = Localize.GetLegacy(key))
                .AddTo(this);
        }
        
        public void SetKey(string key, params string[] addContents)
        {
            this.key = key;
            var text = string.Format(Localize.GetLegacy(key), addContents);
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            _text.text = text;
            _text.ForceMeshUpdate();
        }
    }
}
