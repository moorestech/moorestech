using System;
using UniRx;

namespace Client.Input.KeyPress
{
    /// <summary>
    /// 画面が InputKey のキーを受ける唯一の口。押下の瞬間（購読）と「このフレームに押されたか」（読み）の 2 つの読み方を持ち、入力欄のフォーカス中の抑止と ESC ラッチは InputKey の InputSuppressionFilter が持つ（A-246）
    /// The single port through which screens receive an InputKey; it offers the press moment (subscription) and "pressed this frame" (read), and suppression while a text field has focus plus the ESC latch live in the InputKey's InputSuppressionFilter (A-246)
    /// </summary>
    public interface IUiKeyPressSource
    {
        IObservable<Unit> OnPressed { get; }
        bool WasPressedThisFrame();
    }

    // DI の鍵になるキーごとの型（前例 IUiCloseKeyProbe・IHorizontalArrowKeySource。同じ口を受け手ごとに別のキーで渡すため）
    // Per-key types that serve as DI keys (precedent IUiCloseKeyProbe, IHorizontalArrowKeySource; each receiver gets the same port bound to a different key)
    public interface ISkitAdvanceKeyPressSource : IUiKeyPressSource
    {
    }

    public interface ICloseUiKeyPressSource : IUiKeyPressSource
    {
    }
}
