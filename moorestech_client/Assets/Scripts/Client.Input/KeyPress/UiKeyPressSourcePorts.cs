using System;
using UniRx;

namespace Client.Input.KeyPress
{
    /// <summary>
    /// 画面が InputKey のキーを受ける口。受け手ごとに読むもの 1 つだけを契約に持ち、入力欄のフォーカス中の抑止と ESC ラッチは InputKey の InputSuppressionFilter が持つ（A-246）
    /// Ports through which screens receive an InputKey; each carries only the one reading its receiver uses, and text-field suppression plus the ESC latch live in the InputKey's InputSuppressionFilter (A-246)
    /// </summary>
    /// <remarks>
    /// DI の鍵になるキーごとの型（前例 IUiCloseKeyProbe・IHorizontalArrowKeySource）。両方の読み方は実装の基底 InputKeyPressSource だけが持つ
    /// Per-key types that serve as DI keys (precedent IUiCloseKeyProbe, IHorizontalArrowKeySource); only the implementation base InputKeyPressSource has both readings
    /// </remarks>
    public interface ISkitAdvanceKeyPressSource
    {
        // スキット送りは押下の瞬間を購読する
        // Skit advance subscribes to the press moment
        IObservable<Unit> OnPressed { get; }
    }

    public interface ICloseUiKeyPressSource
    {
        // 閉じるキーはこのフレームに押されたかを読む
        // The close key reads whether it was pressed this frame
        bool WasPressedThisFrame();
    }
}
