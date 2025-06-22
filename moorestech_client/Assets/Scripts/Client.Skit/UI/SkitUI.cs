using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Skit.UI
{
    public class SkitUI : MonoBehaviour
    {
        [SerializeField] private UIDocument skitUiDocument;
        private SkitUITools _skitUITools;
        
        private void Awake()
        {
            _skitUITools = new SkitUITools(skitUiDocument);
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
        
        private void Update()
        {
            _skitUITools.ManualUpdate();
        }
    }
}