using System;
using UniRx;

namespace Client.Ui.State.Inventory.Drag
{
    /// <summary>
    /// マウスの左ボタン（どこで離しても流れる解放と、今押されているか。後者は同じフレームの押下と解放の後先と、解放を取りこぼした分割ドラッグを確かめる）
    /// The left mouse button (release fired wherever it happens, and whether it is pressed now for same-frame ordering and split drags that missed their release)
    /// </summary>
    public interface IPrimaryPointerButton
    {
        IObservable<Unit> OnReleased { get; }
        // マウスのリセット・取り外し等で押下が解放を流さずに消えたとき
        // When the press vanished without a release (mouse reset, removal, etc.)
        IObservable<Unit> OnReset { get; }
        bool IsPressed();
    }
}
