using System;
using UnityEngine;

namespace Client.Input.Pointer
{
    /// <summary>
    /// ポインタ位置の変化の購読口。座標は HybridInput.GetMousePosition と同じ実画面 px・左下原点
    /// Subscription point for pointer position changes; screen pixels with bottom-left origin, same as HybridInput.GetMousePosition
    /// </summary>
    public interface IPointerPositionStream
    {
        IObservable<Vector2> OnPointerScreenPositionChanged { get; }
    }
}
