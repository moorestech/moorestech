using System;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.ProgressBar
{
    public class ProgressBarView : MonoBehaviour
    {
        [SerializeField] private GameObject viewRoot;
        [SerializeField] private Scrollbar scrollbar;
        
        public static ProgressBarView Instance;
        
        private void Awake()
        {
            Instance = this;
            Hide();
        }
        
        public void Show()
        {
            viewRoot.SetActive(true);
        }
        
        public void Hide()
        {
            viewRoot.SetActive(false);
        }
        
        public void SetProgress(float progress)
        {
            scrollbar.size = progress;
        }
    }
}