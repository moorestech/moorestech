using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Interaction
{
    // 押してから離すまでの1回の操作。Dragはしきい値を超えてから呼ばれ、Releaseはドラッグの有無を受け取る
    // One press-to-release gesture; Drag starts after the threshold, Release learns whether a drag happened
    public interface IPointerDragSession
    {
        void Drag(Vector2 world, Vector2 local, bool shift);
        void Release(bool dragged);
    }
}
