using Client.Input;

namespace Client.Ui.Screens.Recipe.Input
{
    /// <summary>
    /// InputManager の CloseUI を正にして押下を読む（A-83 と同じ経路）
    /// Reads the press from InputManager's CloseUI as the authority (same path as A-83)
    /// </summary>
    public sealed class InputManagerCloseKeyProbe : IUiCloseKeyProbe
    {
        public bool WasPressedThisFrame()
        {
            return InputManager.UI.CloseUI.GetKeyDown;
        }
    }
}
