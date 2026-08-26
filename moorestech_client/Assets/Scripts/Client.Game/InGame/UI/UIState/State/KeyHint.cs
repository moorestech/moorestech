using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    ///     画面左下に出す操作ヒント1件。キー名も文言もローカライズキーで持つ
    ///     One key hint for the bottom-left HUD; both the key name and the text are localization keys
    /// </summary>
    public readonly struct KeyHint
    {
        public readonly LocalizationKey KeyNameKey;
        public readonly LocalizationKey TextKey;

        public KeyHint(LocalizationKey keyNameKey, LocalizationKey textKey)
        {
            KeyNameKey = keyNameKey;
            TextKey = textKey;
        }
    }
}
