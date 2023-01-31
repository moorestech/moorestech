using MainGame.UnityView.UI.Builder.BluePrint;
using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class UIBuilderTextObject : MonoBehaviour,IUIBuilderObject
    {
        [SerializeField] private TMP_Text tmpText;

        public IUIBluePrintElement BluePrintElement { get; private set; }

        public void Initialize(IUIBluePrintElement bluePrintElement)
        {
            BluePrintElement = bluePrintElement;
        }

        public void SetText(string text, int fontSize)
        {
            tmpText.text = text;
            tmpText.fontSize = fontSize;
        }

    }
}