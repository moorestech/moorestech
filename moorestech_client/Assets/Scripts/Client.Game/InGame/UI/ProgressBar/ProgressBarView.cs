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

        // 現在の表示状態と進捗。Web UI など外部からの読み取り用
        // Current visibility and progress, for external readers such as the Web UI
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
