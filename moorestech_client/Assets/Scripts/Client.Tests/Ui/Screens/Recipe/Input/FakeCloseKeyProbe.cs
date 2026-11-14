using System;
using Client.Input.KeyPress;
using UniRx;

namespace Client.Tests.Ui.Screens.Recipe.Input
{
    /// <summary>
    /// SetPressed で決めた値をそのまま返す CloseUI の口の偽（購読は流さない）
    /// Fake CloseUI port that returns the value set by SetPressed (the subscription never fires)
    /// </summary>
    public sealed class FakeCloseUiKeyPressSource : ICloseUiKeyPressSource
    {
        private bool _pressed;

        public IObservable<Unit> OnPressed => Observable.Never<Unit>();

        public void SetPressed(bool pressed)
        {
            _pressed = pressed;
        }

        public bool WasPressedThisFrame()
        {
            return _pressed;
        }
    }
}
