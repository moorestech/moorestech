using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Skit.UI
{
    public class SkitUITools
    {
        private readonly UIDocument _skitUiDocument;
        
        private bool _isUIHidden = false;
        
        public SkitUITools(UIDocument skitUiDocument)
        {
            _skitUiDocument = skitUiDocument;
            
            GetButton("HiddenButton").clicked += HideUI;
            
            #region Intenral
            
            Button GetButton(string buttonName)
            {
                return skitUiDocument.rootVisualElement.Q<Button>(buttonName);
            }
            
            #endregion
        }
        
        
        private void HideUI()
        {
            _isUIHidden = true;
            _skitUiDocument.rootVisualElement.style.display = DisplayStyle.None;
        }

        
        public void ManualUpdate()
        {
            //TODO InputManagerに移す
            if (_isUIHidden && Input.GetKeyDown(KeyCode.Escape))
            {
                _skitUiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            }
        }
        
        
    }
}