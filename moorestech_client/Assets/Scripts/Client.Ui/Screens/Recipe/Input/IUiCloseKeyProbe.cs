namespace Client.Ui.Screens.Recipe.Input
{
    /// <summary>
    /// このフレームに CloseUI（Esc）が押されたかを読む口
    /// Port that reads whether CloseUI (Esc) was pressed this frame
    /// </summary>
    public interface IUiCloseKeyProbe
    {
        bool WasPressedThisFrame();
    }
}
