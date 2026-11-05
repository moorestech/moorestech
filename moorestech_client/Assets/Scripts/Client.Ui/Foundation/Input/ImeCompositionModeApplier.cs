using System;
using Client.Input;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// IME モードを「どこかの入力欄にフォーカスがあれば On、無ければ Off」に 1 か所で決める
    /// Decides the IME mode in one place: On while any text field (GameUi or Web) has focus, Off otherwise
    /// </summary>
    public class ImeCompositionModeApplier : IInitializable, IDisposable
    {
        private IDisposable _subscription;

        public void Initialize()
        {
            // 起動時の CEF の On 固定をすぐ現在値で上書きする
            // Immediately override CEF's startup On with the current value
            Apply(UiInputExclusivity.IsTextInputFocused);

            // Web の値は WS スレッドから来るのでメインスレッドで適用する。TextField のフォーカスアウトで Auto に戻る処理より後に効く
            // Web values arrive on the WS thread, so apply on the main thread; this also lands after TextField's focus-out reset to Auto
            _subscription = UiInputExclusivity.TextInputFocused
                .ObserveOnMainThread()
                .Subscribe(Apply);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private static void Apply(bool textInputFocused)
        {
            // Auto は OS 任せでワールド操作中にも変換が始まりうるので、フォーカスが無いときは Off にする
            // Auto leaves it to the OS and can start conversions during world play, so use Off when nothing has focus
            UnityEngine.Input.imeCompositionMode = textInputFocused ? IMECompositionMode.On : IMECompositionMode.Off;
        }
    }
}
