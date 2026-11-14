// moorestech_client/Assets/Scripts/Client.Ui/Components/Text/KeyCap/KeyCap.cs
using Client.Ui.Foundation.Text.WrapSafe;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Text
{
    /// <summary>
    /// kbd 1 個。枠なし・高コントラスト色
    /// One kbd; no box, high-contrast color
    /// </summary>
    public sealed class KeyCap : WrapSafeLabel
    {
        public KeyCap(string keyText)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("key-cap");
            SetKeyText(keyText);
        }

        // キー名は翻訳しない（入力設定の表示名をそのまま出す）
        // Key names are not translated (the input binding's display name is shown as is)
        public void SetKeyText(string keyText)
        {
            text = keyText;
        }
    }
}
