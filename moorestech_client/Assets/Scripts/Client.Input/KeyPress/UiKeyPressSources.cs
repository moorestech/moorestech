namespace Client.Input.KeyPress
{
    // スキットの送りの Enter（テンキーの Enter を含む。A-83）
    // Skit-advance Enter, including the keypad Enter (A-83)
    public sealed class SkitAdvanceKeyPressSource : InputKeyPressSource, ISkitAdvanceKeyPressSource
    {
        public SkitAdvanceKeyPressSource() : base(InputManager.UI.SkitAdvance)
        {
        }
    }

    // 画面を閉じる CloseUI（Esc）
    // CloseUI (Esc), which closes screens
    public sealed class CloseUiKeyPressSource : InputKeyPressSource, ICloseUiKeyPressSource
    {
        public CloseUiKeyPressSource() : base(InputManager.UI.CloseUI)
        {
        }
    }
}
