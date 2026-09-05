using System;
using UniRx;

namespace Client.Game.InGame.UI.Crosshair
{
    /// <summary>
    ///     一人称視点の画面中央クロスヘアの表示フラグ。描画は Web UI が担う
    ///     Visibility flag of the first-person center crosshair; the Web UI renders it
    /// </summary>
    public class CrosshairVisibility
    {
        private readonly ReactiveProperty<bool> _visible = new(false);

        public IObservable<bool> OnVisibleChanged => _visible;
        public bool IsVisible() => _visible.Value;

        public void SetVisible(bool visible)
        {
            _visible.Value = visible;
        }
    }
}
