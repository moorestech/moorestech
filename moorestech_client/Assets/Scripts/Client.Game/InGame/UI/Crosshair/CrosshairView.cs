// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using UnityEngine;
using System;
using Client.Game.InGame.UI.UIState;
using UniRx;

namespace Client.Game.InGame.UI.Crosshair
{
    /// <summary>
    ///     FPS視点の画面中央クロスヘア
    ///     Center-screen crosshair for the first-person view
    /// </summary>
    public class CrosshairView : MonoBehaviour
    {
        private static CrosshairView _instance;
        public static CrosshairView Instance => _instance;

        [SerializeField] private GameObject dotObject;
        private readonly ReactiveProperty<bool> _visible = new(false);

        public IObservable<bool> OnVisibleChanged => _visible;
        public bool IsVisible() => _visible.Value;

        private void Awake()
        {
            _instance = this;
            dotObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            _visible.Value = visible;
            dotObject.SetActive(visible && !WebUiScreenGate.IsWebUiMode);
        }
    }
}
