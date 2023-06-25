using TMPro;
using UniRx;
using UnityEngine;

namespace MainGame.Localization
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextMeshProLocalize : MonoBehaviour
    {
        [SerializeField] private string key;
        
        private void Awake()
        {
            GetComponent<TextMeshProUGUI>().text = Localize.Get(key);
            Localize.OnLanguageChanged.
                Subscribe(_ => GetComponent<TextMeshProUGUI>().text = Localize.Get(key))
                .AddTo(this);
        }
        
        
        public void SetKey(string key)
        {
            this.key = key;
            GetComponent<TextMeshProUGUI>().text = Localize.Get(key);
        }
    }
}