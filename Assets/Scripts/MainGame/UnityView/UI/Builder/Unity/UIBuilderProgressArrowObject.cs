using MainGame.UnityView.UI.Builder.Element;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class UIBuilderProgressArrowObject : MonoBehaviour,IUIBuilderObject
    {
        public IUIBluePrintElement BluePrintElement { get; private set; }
        public void Initialize(IUIBluePrintElement bluePrintElement)
        {
            BluePrintElement = bluePrintElement;
        }
    }
}