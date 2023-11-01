using JetBrains.Annotations;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Builder.Element;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public interface IUIBuilderObject
    {
        /// <summary>
        /// ブループリントから作られた場合、自分を作ったもとになったブループリントがセットされている
        /// </summary>
        public IUIBluePrintElement BluePrintElement { get; }

        public RectTransform RectTransform { get; }
        
        public void Initialize(IUIBluePrintElement bluePrintElement);
    }
}