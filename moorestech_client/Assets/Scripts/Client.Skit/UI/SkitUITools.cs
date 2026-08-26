using Client.Skit.Skit;
using UnityEngine.UIElements;

namespace Client.Skit.UI
{
    public class SkitUITools
    {
        private readonly UIDocument _skitUiDocument;
        
        public SkitUITools(UIDocument skitUiDocument, ISkitActionController skitActionController)
        {
            _skitUiDocument = skitUiDocument;
            
            GetButton("HiddenButton").clicked += HideUI;
            GetButton("SkipButton").clicked += () => skitActionController.SetSkip(true);
            
            var autoButton = GetButton("AutoButton");
            SetAutoButtonView(skitActionController.IsAuto);
            autoButton.clicked += () =>
            {
                var isAuto = !skitActionController.IsAuto;
                skitActionController.SetAuto(isAuto);
                SetAutoButtonView(isAuto);
            };
            
            #region Intenral
            
            Button GetButton(string buttonName)
            {
                return skitUiDocument.rootVisualElement.Q<Button>(buttonName);
            }
            
            void SetAutoButtonView(bool isAuto)
            {
                var addClass = isAuto ? "AutoEnable" : "AutoDisable";
                var removeClass = isAuto ? "AutoDisable" : "AutoEnable";
                autoButton.AddToClassList(addClass);
                autoButton.RemoveFromClassList(removeClass);
            }
            
            #endregion
        }
        
        
        private void HideUI()
        {
            _skitUiDocument.rootVisualElement.style.display = DisplayStyle.None;
        }
    }
}