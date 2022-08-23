using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class UIBuilderTextObject : MonoBehaviour
    {
        [SerializeField] private TMP_Text tmpText;

        public void SetText(string text, int fontSize)
        {
            tmpText.text = text;
            tmpText.fontSize = fontSize;
        }
        
    }
}