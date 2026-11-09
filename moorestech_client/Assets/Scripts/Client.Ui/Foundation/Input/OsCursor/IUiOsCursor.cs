using System;

namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// OS カーソルの形を要求する唯一の口（USS はビットマップを参照できないため C# から当てる。D-12）。生きている要求のうち一番上の層の、その層で最後に取ったものの形が当たり、Dispose で外れる
    /// The only port that requests an OS cursor shape (USS may not reference bitmaps, so C# applies it; D-12); among live requests the latest one in the highest layer wins, and Dispose withdraws it
    /// </summary>
    /// <remarks>
    /// 要求は必ず Dispose する（外し損ねた上の層の要求は以後ずっと下の層に勝つ）。要素に紐づく要求は UiOsCursorHoverRequest を使う（PointerLeave・取り外し・大きさ 0 で外す）
    /// Always Dispose a request (a leaked upper-layer request keeps beating lower layers from then on); element-bound requests use UiOsCursorHoverRequest (withdrawn on PointerLeave, detach and zero size)
    /// </remarks>
    public interface IUiOsCursor
    {
        IDisposable Request(UiOsCursorShape shape, UiOsCursorLayer layer);
    }
}
