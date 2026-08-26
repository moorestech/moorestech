using Client.Skit.Skit;
using UnityEngine.UIElements;

namespace Client.Skit.UI
{
    public class SkitUITools
    {
        private readonly UIDocument _skitUiDocument;
        
        internal bool IsUIHidden { get; private set; }
        
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
            IsUIHidden = true;
            _skitUiDocument.rootVisualElement.style.display = DisplayStyle.None;
        }

        
        // 非表示にした会話UIを戻す。Esc判定はUIステート側（SkitPlayingSubState）が持つ
        // Restore the hidden dialogue UI. The Esc decision lives in the UI state (SkitPlayingSubState)
        internal void ShowUI()
        {
            IsUIHidden = false;
            _skitUiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        }
    }
}