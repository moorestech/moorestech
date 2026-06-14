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

        // 表示状態と進捗の外部読み取り用
        // Visibility and progress, for external readers
        public bool IsShown => viewRoot.activeSelf;
        public float CurrentProgress => scrollbar.size;

        // Show/Hide/SetProgress いずれかで状態が変化したら発火する
        // Fires whenever Show/Hide/SetProgress changes the state
        public event Action OnProgressChanged;

        private void Awake()
        {
            Instance = this;
            Hide();
        }

        public void Show()
        {
            viewRoot.SetActive(true);
            OnProgressChanged?.Invoke();
        }

        public void Hide()
        {
            viewRoot.SetActive(false);
            OnProgressChanged?.Invoke();
        }

        public void SetProgress(float progress)
        {
            scrollbar.size = progress;
            OnProgressChanged?.Invoke();
        }
    }
}
