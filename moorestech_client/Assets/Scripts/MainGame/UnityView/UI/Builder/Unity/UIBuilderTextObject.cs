using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Builder.Element;
using TMPro;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class UIBuilderTextObject : MonoBehaviour,IUIBuilderObject
    {
        [SerializeField] private TMP_Text tmpText;

        public IUIBluePrintElement BluePrintElement { get; private set; }
        public RectTransform RectTransform { get; private set; }

        public void Initialize(IUIBluePrintElement bluePrintElement)
        {
            RectTransform = GetComponent<RectTransform>();
            BluePrintElement = bluePrintElement;
        }

        public void SetText(string text, int fontSize)
        {
            tmpText.text = text;
            tmpText.fontSize = fontSize;
        }

    }
}