using System;
using Client.Input;
using UniRx;

namespace Client.Ui.Screens.Skit
{
    /// <summary>
    /// スキットの送りの Enter（テンキーの Enter を含む）を InputManager の InputKey から受ける。GameUi は入力欄以外にフォーカスを持たせないため、UITK のキーイベントは会話窓に届かない（D-11・D-29）
    /// Receives the skit-advance Enter (including the keypad Enter) from the InputManager InputKey; GameUi gives focus only to text fields, so UITK key events never reach the dialogue window (D-11, D-29)
    /// </summary>
    public static class SkitEnterKeySource
    {
        // 入力欄フォーカス中の抑止とそのログは InputKey の InputSuppressionFilter が持つので、ここでは判定しない
        // Suppression while a text field has focus, and its log, belong to the InputKey's InputSuppressionFilter, so nothing is judged here
        public static IObservable<Unit> ObservePressed()
        {
            var skitAdvance = InputManager.UI.SkitAdvance;
            return Observable.FromEvent(handler => skitAdvance.OnGetKeyDown += handler, handler => skitAdvance.OnGetKeyDown -= handler);
        }
    }
}
