using Client.Ui.Screens.Recipe.Input;

namespace Client.Tests.Ui.Screens.Recipe.Input
{
    /// <summary>
    /// SetPressed で決めた値をそのまま返す CloseUI の偽
    /// Fake CloseUI probe that returns the value set by SetPressed
    /// </summary>
    public sealed class FakeCloseKeyProbe : IUiCloseKeyProbe
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
