using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.KeyControl
{
    public class KeyControlDescription : MonoBehaviour
    {
        public static KeyControlDescription Instance { get; private set; }
        
        [SerializeField] private TMP_Text keyControlText;
        
        private void Awake()
        {
            Instance = this;
        }
        
        public void SetText(string text)
        {
            if (keyControlText != null)
            {
                keyControlText.text = text;
            }
        }
    }
}