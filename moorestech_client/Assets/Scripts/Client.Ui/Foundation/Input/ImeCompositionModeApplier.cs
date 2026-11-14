using Client.Input;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 入力欄フォーカスで IME を On/Off する
    /// Switch IME On/Off by text field focus
    /// </summary>
    public class ImeCompositionModeApplier : IInitializable
    {

        public void Initialize()
        {
            // 起動直後の値を反映する
            // Apply the value right after boot
            Apply(UiInputExclusivity.IsTextInputFocused);

            // メインスレッドで適用し、TextField のフォーカスアウトで Auto に戻る処理より後に効かせる
            // Apply on the main thread so it lands after TextField's focus-out reset to Auto
            UiInputExclusivity.TextInputFocused
                .ObserveOnMainThread()
                .Subscribe(Apply);
        }

        private static void Apply(bool textInputFocused)
        {
            // Auto は OS 任せでワールド操作中にも変換が始まりうるので、フォーカスが無いときは Off にする
            // Auto leaves it to the OS and can start conversions during world play, so use Off when nothing has focus
            UnityEngine.Input.imeCompositionMode = textInputFocused ? IMECompositionMode.On : IMECompositionMode.Off;
        }
    }
}
