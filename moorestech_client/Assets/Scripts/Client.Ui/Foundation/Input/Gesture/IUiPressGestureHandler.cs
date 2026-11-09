using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input.Gesture
{
    /// <summary>
    /// 押下の分類の受け手（座標はパネル px）
    /// Receiver of press classification (panel px)
    /// </summary>
    public interface IUiPressGestureHandler
    {
        void OnPressStarted(Vector2 panelPosition);
        void OnDragMoved(UiPressMove move);
        void OnDragEnded(Vector2 panelPosition);
        void OnTapped(VisualElement pressTarget);
        void OnAborted(bool dragged);
    }
}
