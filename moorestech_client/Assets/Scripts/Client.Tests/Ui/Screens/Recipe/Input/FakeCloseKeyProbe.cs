using Client.Input.KeyPress;

namespace Client.Tests.Ui.Screens.Recipe.Input
{
    /// <summary>
    /// SetPressed で決めた値をそのまま返す CloseUI の口の偽
    /// Fake CloseUI port that returns the value set by SetPressed
    /// </summary>
    public sealed class FakeCloseUiKeyPressSource : ICloseUiKeyPressSource
    {
        private bool _pressed;

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
