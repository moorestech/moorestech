using System;
using UniRx;
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
        public IObservable<Unit> OnProgressChanged => _onProgressChanged;
        private readonly Subject<Unit> _onProgressChanged = new();

        private void Awake()
        {
            Instance = this;
            Hide();
        }

        public void Show()
        {
            viewRoot.SetActive(true);
            _onProgressChanged.OnNext(Unit.Default);
        }

        public void Hide()
        {
            viewRoot.SetActive(false);
            _onProgressChanged.OnNext(Unit.Default);
        }

        public void SetProgress(float progress)
        {
            scrollbar.size = progress;
            _onProgressChanged.OnNext(Unit.Default);
        }
    }
}
