using UnityEngine;

namespace Client.Ui.Foundation.Input.Gesture
{
    /// <summary>
    /// ドラッグ中の 1 回の移動。押下点からの全量と前回からの増分（パネル px）
    /// One drag move: the whole delta from the press point and the increment since the last move (panel px)
    /// </summary>
    public readonly struct UiPressMove
    {
        public Vector2 FromStart { get; }
        public Vector2 SinceLast { get; }

        public UiPressMove(Vector2 fromStart, Vector2 sinceLast)
        {
            FromStart = fromStart;
            SinceLast = sinceLast;
        }
    }
}
