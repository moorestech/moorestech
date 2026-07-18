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
