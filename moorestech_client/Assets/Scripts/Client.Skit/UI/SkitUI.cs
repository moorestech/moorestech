using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Skit.UI
{
    public class SkitUI : MonoBehaviour
    {
        [SerializeField] private UIDocument skitUiDocument;
        
        private void Awake()
        {
            GetButton("HiddenButton").clicked += () =>
            {
                
            };
            
            #region Intenral
            
            Button GetButton(string buttonName)
            {
                return skitUiDocument.rootVisualElement.Q<Button>(buttonName);
            }
            
            #endregion
        }
        
        public void SetText(string characterName, string text)
        {
        }
        
        public void ShowTransition(bool isShow, float duration)
        {
        }
        
        public void ShowSelectionUI(bool enable)
        {
        }
        
        public async UniTask<int> WaitSelectText(List<string> texts)
        {
            return -1;
        }
    }
}