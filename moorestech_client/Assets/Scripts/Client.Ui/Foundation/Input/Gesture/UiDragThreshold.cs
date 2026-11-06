using System;

namespace Client.Ui.Foundation.Input.Gesture
{
    /// <summary>
    /// 押下のジェスチャの純粋計算（Web shared/pointerGesture/dragThreshold.ts の写し）
    /// Pure pointer-gesture math (port of the Web shared/pointerGesture/dragThreshold.ts)
    /// </summary>
    public static class UiDragThreshold
    {
        // タップ/ドラッグの境（5px）
        // Tap/drag boundary (5px)
        public const float ThresholdPx = 5f;

        // 押下点からの移動が閾値以上になったらドラッグ確定
        // A move of at least the threshold from the press point commits to a drag
        public static bool Exceeded(float dx, float dy)
        {
            return ThresholdPx <= Math.Sqrt((double)dx * dx + (double)dy * dy);
        }

        // 掴んだ点を基準に、ポインタを下へ動かすと内容も下へ流れるパン量
        // Natural pan: moving the pointer down from the grabbed point slides the content down
        public static float NextScrollTop(float startScrollTop, float movedY)
        {
            return startScrollTop - movedY;
        }
    }
}
