// moorestech_client/Assets/Scripts/Client.Ui/Components/Text/KeyCap/KeyCap.cs
using UnityEngine.UIElements;

namespace Client.Ui.Components.Text
{
    /// <summary>
    /// kbd 1 個。Web は枠を描かず字形を継ぎ、色だけ高コントラストにする（app/tokens.css の :where(.keyHintText) kbd）
    /// One kbd; the Web draws no box, inherits the font and only forces the high-contrast color (app/tokens.css :where(.keyHintText) kbd)
    /// </summary>
    public sealed class KeyCap : Label
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
