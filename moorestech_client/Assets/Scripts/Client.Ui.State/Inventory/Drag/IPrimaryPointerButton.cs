using System;
using UniRx;

namespace Client.Ui.State.Inventory.Drag
{
    /// <summary>
    /// マウスの左ボタン（どこで離しても流れる解放と、今押されているか。後者は同じフレームの押下と解放の後先を確かめる。D-20）
    /// The left mouse button (release fired wherever it happens, and whether it is pressed now for same-frame ordering; D-20)
    /// </summary>
    public interface IPrimaryPointerButton
    {
        IObservable<Unit> OnReleased { get; }
        bool IsPressed();
    }
}
