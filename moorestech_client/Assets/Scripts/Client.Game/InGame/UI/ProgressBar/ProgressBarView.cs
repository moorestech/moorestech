using System;
using Client.Game.InGame.UI.UIState;
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

        // 論理状態はWebモードでも維持し、uGUI描画だけをゲートで抑止する（ProgressTopicのデータ源のため）
        // Keep the logical state alive in web mode and gate only the uGUI rendering (this view feeds ProgressTopic)
        private bool _isShown;

        // 表示状態と進捗の外部読み取り用
        // Visibility and progress, for external readers
        public bool IsShown => _isShown;
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
            _isShown = true;
            viewRoot.SetActive(!WebUiScreenGate.IsWebUiMode);
            _onProgressChanged.OnNext(Unit.Default);
        }

        public void Hide()
        {
            _isShown = false;
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
